﻿using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Windows.Storage;
using Microsoft.Toolkit.Uwp.Notifications;
using System.Text.RegularExpressions;
using System.Diagnostics;
using RestSharp;

namespace Data
{
    public class WebApi
    {
        ApplicationDataContainer localSettings = ApplicationData.Current.LocalSettings;
        private HttpClient QRCodeClient;
        public HttpClient Client;
        public static UserInfo UserInfo;
        public static QRCodeInfo QRCodeInfo;
        public static bool isEnterHiddenMode;
        public TokenInfo TokenInfo;

        public WebApi(bool useCookie=true)
        {
            if (useCookie)
            {
                InitializeInternet();
            }
        }

        /// <summary>
        /// 添加user-agent和cookie
        /// </summary>
        public void InitializeInternet()
        {
            var handler = new HttpClientHandler { UseCookies = false };
            Client = new HttpClient(handler);
            Client.DefaultRequestHeaders.Add("user-agent", "Mozilla/5.0 (Windows NT 10.0; WOW64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/83.0.4103.61 Safari/537.36 115Browser/25.0.1.0");

            var cookie = AppSettings._115_Cookie;

            //cookie不为空且可用
            if (!string.IsNullOrEmpty(cookie))
            {
                Client.DefaultRequestHeaders.Add("Cookie", cookie);
            }
        }

        /// <summary>
        /// 更新cookie
        /// </summary>
        /// <param name="cookie"></param>
        public void RefreshCookie(string cookie)
        {
            Client.DefaultRequestHeaders.Remove("Cookie");
            Client.DefaultRequestHeaders.Add("Cookie", cookie);
        }

        /// <summary>
        /// 检查登录状态
        /// </summary>
        /// <returns>true - 登录，false - 未登录</returns>
        public async Task<bool> UpdateLoginInfo()
        {
            bool result = false;
            //Uri uri = new Uri("https://webapi.115.com/files?aid=1&cid=2223208807868137192&o=user_ptime&asc=0&offset=0&show_dir=1&limit=56&code=&scid=&snap=0&natsort=1&record_open_time=1&source=&format=json");
            HttpResponseMessage response;
            try
            {
                response = await Client.GetAsync($"https://my.115.com/?ct=ajax&ac=nav&callback=jQuery172046995607070659906_1647774910536&_={DateTimeOffset.Now.ToUnixTimeSeconds()}");
            }
            catch (HttpRequestException e)
            {
                FileMatch.tryToast("网络异常", "检查115登录状态时出现异常：", e.Message);

                return result;
            }

            if (!response.IsSuccessStatusCode)
            {
                return result;
            }

            string strReuslt = await response.Content.ReadAsStringAsync();
            strReuslt = strReuslt.Replace("jQuery172046995607070659906_1647774910536(", "");
            strReuslt = strReuslt.Replace(");", "");

            try
            {
                UserInfo = JsonConvert.DeserializeObject<UserInfo>(strReuslt);
                result = UserInfo.state;
            }
            catch (Exception)
            {
                result = false;
            }
            return result;
        }

        /// <summary>
        /// 检查是否为隐藏模式
        /// </summary>
        /// <returns></returns>
        public async Task<bool> IsHiddenModel()
        {
            var result = false;

            var values = new Dictionary<string, string>
                {
                    { "last_file_type", " folder"},
                    {"last_file_id"," 1865386445801900763" }
                };
            var content = new FormUrlEncodedContent(values);

            HttpResponseMessage response;
            try
            {
                response = Client.PostAsync("https://115.com/?ac=setting&even=saveedit&is_wl_tpl=1", content).Result;
            }
            catch (AggregateException e)
            {
                FileMatch.tryToast("网络异常","检查115隐藏状态时出现异常：",e.Message);

                return result;
            }

            if (!response.IsSuccessStatusCode)
            {
                return result;
            }

            var strResult = await response.Content.ReadAsStringAsync();
            _115Setting DriveSetting;
            try
            {
                DriveSetting = JsonConvert.DeserializeObject<_115Setting>(strResult);
            }
            catch
            {
                return result;
            }

            if (DriveSetting.data.show == "1")
            {
                result = true;
                isEnterHiddenMode = true;
            }
            else
            {
                isEnterHiddenMode = false;
            }


            return result;
        }



        /// <summary>
        /// 导入CidList获取到的所有信息到数据库
        /// </summary>
        /// <param name="cidList"></param>
        /// <param name="getFilesProgressInfo"></param>
        /// <param name="progress"></param>
        /// <returns></returns>
        public async Task GetAllFileInfoToDataAccess(List<string> cidList, GetFilesProgressInfo getFilesProgressInfo, CancellationToken token, IProgress<GetFileProgessIProgress> progress = null)
        {
            foreach (var cid in cidList)
            {
                if (token.IsCancellationRequested)
                {
                    Debug.WriteLine("退出3");
                    return;
                }
                //fileProgressInfo.datumList = new List<Datum>();

                //统计发送请求的频率
                int sendCount = 0;
                long nowDate = DateTimeOffset.Now.ToUnixTimeSeconds();

                await Task.Delay(1000);

                // 一开始只有cid，先获取cid的属性
                var cidCategory = await GetFolderCategory(cid);
                sendCount++;

                //正常不为空，为空说明有异常
                if (cidCategory == null)
                {
                    progress.Report(new GetFileProgessIProgress() { getFilesProgressInfo = getFilesProgressInfo, status = ProgressStatus.error, sendCountPerMinutes = 1 });

                    // 退出
                    return;
                }

                // 该文件已存在数据库里
                if (DataAccess.IsLastestFileDataExists(cidCategory.pick_code, cidCategory.utime) && Data.StaticData.isJumpExistsFolder)
                {
                    //统计上下级文件夹所含文件的数量
                    //文件数量
                    getFilesProgressInfo.FilesCount += cidCategory.count;
                    //文件夹数量
                    getFilesProgressInfo.FolderCount += cidCategory.folder_count;

                    var cpm = sendCount * 60 / (DateTimeOffset.Now.ToUnixTimeSeconds() - nowDate);
                    progress.Report(new GetFileProgessIProgress() { getFilesProgressInfo = getFilesProgressInfo, sendCountPerMinutes = cpm });

                }
                //之前未添加
                else
                {
                    //获取当前文件夹下所有文件信息，并添加到数据库中
                    getFilesProgressInfo = await TraverseAllFileInfo(cid, getFilesProgressInfo, token, progress);

                    if (getFilesProgressInfo == null) continue;

                    //不添加有错误的目录进数据库（添加数据库时会跳过已经添加过的目录，对于出现错误的目录不添加方便后续重新添加）
                    if(getFilesProgressInfo.FailCid.Count == 0)
                    {
                        DataAccess.AddFilesInfo(FolderCategory.ConvertFolderToDatum(cidCategory, cid));
                    }
                }
            }

            if (token.IsCancellationRequested)
            {
                progress.Report(new GetFileProgessIProgress() { getFilesProgressInfo = getFilesProgressInfo, status = ProgressStatus.cancel, sendCountPerMinutes = 1 });
            }
            else
            {
                // 完成
                progress.Report(new GetFileProgessIProgress() { getFilesProgressInfo = getFilesProgressInfo, status = ProgressStatus.done, sendCountPerMinutes = 1 });
            }

        }

        /// <summary>
        /// 获取所有文件信息
        /// </summary>
        /// <param name="cid"></param>
        /// <param name="webFileInfoList"></param>
        /// <returns></returns>
        public async Task<GetFilesProgressInfo> TraverseAllFileInfo(string cid, GetFilesProgressInfo getFilesProgressInfo, CancellationToken token, IProgress<GetFileProgessIProgress> progress = null)
        {
            //var webFileInfoList = fileProgressInfo.datumList;
            if (token.IsCancellationRequested)
            {
                return null;
                Debug.WriteLine("退出1");
            }


            //统计请求速度
            int sendCount = 0;
            long nowDate = DateTimeOffset.Now.ToUnixTimeSeconds();

            //successCount++;
            await Task.Delay(1000);

            //查询下一级文件信息
            var WebFileInfo = GetFile(cid);
            sendCount++;

            if (WebFileInfo.state)
            {
                foreach (var item in WebFileInfo.data)
                {
                    if (token.IsCancellationRequested)
                    {
                        return null;
                        Debug.WriteLine("退出2");
                    }

                    //文件夹
                    if (item.fid == null)
                    {
                        getFilesProgressInfo.FolderCount++;

                        //查询数据库是否存在
                       if (DataAccess.IsLastestFileDataExists(item.pc, item.te) && Data.StaticData.isJumpExistsFolder)
                        {
                            //统计下级文件夹所含文件的数量
                            //通过数据库获取
                            var datumList = DataAccess.GetAllFilesTraverse(item.cid);

                            //文件数量
                            getFilesProgressInfo.FilesCount += datumList.Count;

                            var cpm = sendCount * 60 / (DateTimeOffset.Now.ToUnixTimeSeconds() - nowDate);
                            progress.Report(new GetFileProgessIProgress() { getFilesProgressInfo = getFilesProgressInfo, sendCountPerMinutes = cpm });
                        }
                        else
                        {
                            //先添加文件后添加文件夹
                            getFilesProgressInfo = await TraverseAllFileInfo(item.cid, getFilesProgressInfo, token, progress);

                            if (getFilesProgressInfo == null) continue;

                            DataAccess.AddFilesInfo(item);
                        }
                    }
                    //文件
                    else
                    {
                        getFilesProgressInfo.FilesCount++;

                        var cpm = sendCount * 60 / (DateTimeOffset.Now.ToUnixTimeSeconds() - nowDate);
                        progress.Report(new GetFileProgessIProgress() { getFilesProgressInfo = getFilesProgressInfo, sendCountPerMinutes = cpm });

                        DataAccess.AddFilesInfo(item);

                        //webFileInfoList.Add(item);
                    }
                }
            }
            else
            {
                getFilesProgressInfo.FailCid.Add(cid);
            }

            return getFilesProgressInfo;
        }

        //public async Task<>

        /// <summary>
        /// 获取文件信息
        /// </summary>
        /// <param name="cid"></param>
        /// <param name="limit"></param>
        /// <returns></returns>
        public WebFileInfo GetFile(string cid, int limit = 40,bool userApi2=false)
        {
            WebFileInfo WebFileInfoResult = new();

            string url;
            if (!userApi2)
            {
                url = $"https://webapi.115.com/files?aid=1&cid={cid}&o=user_ptime&asc=0&offset=0&show_dir=1&limit={limit}&code=&scid=&snap=0&natsort=1&record_open_time=1&source=&format=json";
            }
            else
            {
                //旧接口只有t，没有修改时间（te），创建时间（tp）
                url = $"https://aps.115.com/natsort/files.php?aid=1&cid={cid}&o=file_name&asc=1&offset=0&show_dir=1&limit={limit}&code=&scid=&snap=0&natsort=1&record_open_time=1&source=&format=json&fc_mix=0&type=&star=&is_share=&suffix=&custom_order=";
            }

            HttpResponseMessage response;
            try
            {
                response = Client.GetAsync(url).Result;
            }
            catch (AggregateException e)
            {
                FileMatch.tryToast("网络异常", "获取文件信息时出现异常：", e.Message);
                return WebFileInfoResult;
            }

            if (!response.IsSuccessStatusCode)
            {
                return WebFileInfoResult;
            }
            var strResult = response.Content.ReadAsStringAsync().Result;

            WebFileInfoResult = JsonConvert.DeserializeObject<WebFileInfo>(strResult);

            //te，tp简单用t替换，接口2没有te,tp
            if (userApi2)
            {
                foreach(var item in WebFileInfoResult.data)
                {
                    //item.t 可能是 "1658999027" 也可能是 "2022-07-28 17:03"

                    //"1658999027"
                    if (FileMatch.isNumberic1(item.t))
                    {
                        int dateInt = Int32.Parse(item.t);
                        item.te = item.tp = dateInt;
                        item.t = FileMatch.ConvertInt32ToDateTime(dateInt);
                    }
                    //"2022-07-28 17:03"
                    else
                    {
                        int dateInt = FileMatch.ConvertDateTimeToInt32(item.t);
                        item.te = item.tp = dateInt;
                    }


                }
            }

            //接口1出错，使用接口2
            if (WebFileInfoResult.errNo == 20130827 && userApi2 == false)
            {
                WebFileInfoResult = GetFile(cid, limit, true);
            }
            //未加载全部
            else if(WebFileInfoResult.count > limit)
            {
                WebFileInfoResult = GetFile(cid, WebFileInfoResult.count, userApi2);
            }

            return WebFileInfoResult;
        }

        /// <summary>
        /// 获取文件夹属性（含大小和数量）
        /// </summary>
        /// <param name="cid"></param>
        /// <param name="limit"></param>
        /// <returns></returns>
        public async Task<FolderCategory> GetFolderCategory(string cid)
        {
            FolderCategory WebFileInfoResult=null;

            string url = $"https://webapi.115.com/category/get?cid={cid}";
            HttpResponseMessage response;
            try
            {
                response = await Client.GetAsync(url);
            }
            catch(AggregateException e)
            {
                FileMatch.tryToast("网络异常", "获取文件夹属性时出现异常：", e.Message);
                return WebFileInfoResult;
            }catch(HttpRequestException e)
            {
                FileMatch.tryToast("网络异常", "获取文件夹属性时出现异常：", e.Message);
                return WebFileInfoResult;
            }

            if (response.IsSuccessStatusCode)
            {
                var strResult = await response.Content.ReadAsStringAsync();

                WebFileInfoResult = JsonConvert.DeserializeObject<FolderCategory>(strResult);
            }

            return WebFileInfoResult;
        }

        /// <summary>
        /// 检查二维码登录验证状态，若登录成功则存储cookie;
        /// </summary>
        /// <returns></returns>
        public async Task<TokenInfo> NetworkVerifyTokenAsync()
        {
            try
            {
                var values = new Dictionary<string, string>
                {
                    { "account", QRCodeInfo.data.uid }
                };
                var content = new FormUrlEncodedContent(values);

                if (QRCodeClient == null)
                {
                    QRCodeClient = new HttpClient();
                }

                var response = QRCodeClient.PostAsync("https://passportapi.115.com/app/1.0/web/1.0/login/qrcode", content).Result;

                if (!response.IsSuccessStatusCode)
                {
                    return null;
                }

                string strResult = await response.Content.ReadAsStringAsync();
                TokenInfo = JsonConvert.DeserializeObject<TokenInfo>(strResult);

                if (TokenInfo.state == 1)
                {
                    //存储cookie至本地
                    List<string> CookieList = new List<string>();
                    foreach (var item in TokenInfo.data.cookie.GetType().GetProperties())
                    {
                        CookieList.Add($"{item.Name}={item.GetValue(TokenInfo.data.cookie)}");
                    }

                    var cookie = string.Join(";", CookieList);
                    localSettings.Values["cookie"] = cookie;

                    Client.DefaultRequestHeaders.Add("Cookie", cookie);

                }

            }
            catch (Exception)
            {
                //TokenInfo = null;
            }

            return TokenInfo;
        }

        /// <summary>
        /// 检查二维码扫描状态
        /// </summary>
        /// <returns></returns>
        public async Task<QRCodeStatus> GetQRCodeStatusAsync()
        {
            QRCodeStatus qRCodeStatus = new QRCodeStatus();

            if (QRCodeClient == null)
            {
                QRCodeClient = new HttpClient();
            }
        
            string url = $"https://qrcodeapi.115.com/get/status/?uid={QRCodeInfo.data.uid}&time={QRCodeInfo.data.time}&sign={QRCodeInfo.data.sign}&_={DateTimeOffset.Now.ToUnixTimeSeconds()}";

            try
            {
                var response = await QRCodeClient.GetAsync(url);
                if (!response.IsSuccessStatusCode)
                {
                    return qRCodeStatus;
                }

                var strResult = await response.Content.ReadAsStringAsync();
                qRCodeStatus = JsonConvert.DeserializeObject<QRCodeStatus>(strResult);
            }
            catch (Exception)
            {
                //TokenInfo = null;
            }

            return qRCodeStatus;
        }

        /// <summary>
        /// 获取登录二维码信息
        /// </summary>
        /// <returns></returns>
        public async Task<QRCodeInfo> GetQRCodeInfo()
        {
            QRCodeInfo = new QRCodeInfo();

            if (QRCodeClient == null)
            {
                QRCodeClient = new HttpClient();
            }
            var response = await Client.GetAsync("https://qrcodeapi.115.com/api/1.0/web/1.0/token");
            if (!response.IsSuccessStatusCode)
            {
                return QRCodeInfo;
            }


            var result = await response.Content.ReadAsStringAsync();
            QRCodeInfo = JsonConvert.DeserializeObject<QRCodeInfo>(result);
            return QRCodeInfo;
        }

        /// <summary>
        /// 请求115浏览器下载
        /// </summary>
        /// <param name="videoInfoList"></param>
        public async void RequestDown(List<Datum> videoInfoList)
        {
            var downRequest = new Browser_115_Request();
            //UID
            downRequest.uid = videoInfoList[0].uid;

            //KEY
            if (QRCodeInfo == null)
            {
                await GetQRCodeInfo();
            }
            downRequest.key = QRCodeInfo.data.uid;

            //PARAM
            downRequest.param = new Param_Request();
            downRequest.param.list = new();
            foreach (var videoInfo in videoInfoList)
            {
                bool isdir = videoInfo.uid == 0 ? true : false;
                downRequest.param.list.Add(new Down_Request() { n = videoInfo.n, pc = videoInfo.pc, is_dir = isdir });
            }
            downRequest.param.count = downRequest.param.list.Count;
            downRequest.param.ref_url = $"https://115.com/?cid={videoInfoList[0].cid}&offset=0&mode=wangpan";

            string url = "";
            string jsonString = JsonConvert.SerializeObject(downRequest);

            JObject jObject = JObject.Parse(jsonString);
            IEnumerable<string> nameValues = jObject
                .Properties()
                .Select(x => $"{x.Name}={WebUtility.UrlEncode(x.Value.ToString().Replace(System.Environment.NewLine, string.Empty).Replace(" ", ""))}");

            url += "browser115://download?" + string.Join("&", nameValues);

            // The URI to launch
            var uriDown = new Uri(url);

            // Set the option to show a warning
            var options = new Windows.System.LauncherOptions();
            options.DesiredRemainingView = Windows.UI.ViewManagement.ViewSizePreference.UseLess;

            // Launch the URI
            var success = await Windows.System.Launcher.LaunchUriAsync(uriDown, options);

            if (success)
            {
                // URI launched
            }
            else
            {
                // URI launch failed
            }
        }

        /// <summary>
        /// 检查Cookie是否可用后更新（Client及localSettings）
        /// </summary>
        /// <returns></returns>
        public async Task<bool> tryRefreshCookie(string cookie)
        {
            bool result = false;
            //先保存之前的Cookie，若Cookie无效则恢复原有Cookie
            string currentCookie;

            IEnumerable<string> value;
            bool haveCookie = Client.DefaultRequestHeaders.TryGetValues("Cookie",out value);
            if (haveCookie)
            {
                currentCookie = value.SingleOrDefault();
                RefreshCookie(cookie);

                //使用新Cookie登录不成功，恢复默认值
                result = await UpdateLoginInfo();
                if (!result)
                {
                    RefreshCookie(currentCookie);
                }
                else
                {
                    localSettings.Values["cookie"] = cookie;
                }
            }
            else
            {
                RefreshCookie(cookie);
                result = await UpdateLoginInfo();
                localSettings.Values["cookie"] = cookie;
            }

            return result;
        }

        /// <summary>
        /// 退出账号
        /// </summary>
        public async void LogoutAccount()
        {
            if (UserInfo == null)
            {
                return;
            }

            //退出账号
            await Client.GetAsync("https://passportapi.115.com/app/1.0/web/1.0/logout/logout/?goto=https%3A%2F%2F115.com%2F");

            //清空账号信息
            isEnterHiddenMode = false;
            QRCodeInfo = null;
            UserInfo = null;
            DeleteCookie();
            //if (!response.IsSuccessStatusCode)
            //{
            //    return QRCodeInfo;
            //}


            //var result = await response.Content.ReadAsStringAsync();
            //QRCodeInfo = JsonConvert.DeserializeObject<QRCodeInfo>(result);
        }

        /// <summary>
        /// 删除Cookie
        /// </summary>
        public static void DeleteCookie()
        {
            ApplicationData.Current.LocalSettings.Values["cookie"] = null;
        }

        /// <summary>
        /// 获取下载链接
        /// </summary>
        /// <param name="pickcode"></param>
        /// <returns></returns>
        public List<string> GetDownUrl(string pickcode)
        {
            List<string> downUrlList = new();
            long tm = DateTimeOffset.Now.ToUnixTimeSeconds();
            string src = $"{{\"pickcode\":\"{pickcode}\"}}";
            var item = m115.encode(src, tm);
            byte[] data = item.Item1;
            byte[] keyBytes = item.Item2;

            string dataString = Encoding.ASCII.GetString(data);
            var dataUrlEncode = System.Web.HttpUtility.UrlEncode(dataString);

            var client = new RestClient($"http://proapi.115.com/app/chrome/downurl?t={tm}");
            var request = new RestRequest();
            request.AddHeader("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/104.0.5112.81 Safari/537.36 Edg/104.0.1293.47");
            request.AddHeader("Cookie", AppSettings._115_Cookie);
            request.AddHeader("Content-Type", "application/x-www-form-urlencoded");
            var body = $"data={dataUrlEncode}";
            request.AddParameter("application/x-www-form-urlencoded", body, ParameterType.RequestBody);
            var response = client.Post(request);

            DownUrlBase64EncryptInfo downurl_base64EncryptInfo;
            if (response.IsSuccessful && response.Content != null)
            {
                downurl_base64EncryptInfo = JsonConvert.DeserializeObject<DownUrlBase64EncryptInfo>(response.Content);

                if (downurl_base64EncryptInfo != null)
                {
                    string base64Text = downurl_base64EncryptInfo.data;

                    byte[] srcBase64 = Convert.FromBase64String(base64Text);

                    var rep = m115.decode(srcBase64, keyBytes);

                    //JObject json = JsonConvert.DeserializeObject<JObject>(rep);
                    var json = JObject.Parse(rep);

                    foreach (var children in json)
                    {
                        var videoInfo = children.Value;
                        var downUrl = videoInfo["url"]["url"].ToString();
                        downUrlList.Add(downUrl);
                    }
                }
            }

            return downUrlList;
        }

        /// <summary>
        /// PotPlayer播放
        /// </summary>
        /// <param name="pickCode"></param>
        public async void PlayeByPotPlayer(string pickCode)
        {
            var m3U8Infos = await Getm3u8InfoByPickCode(pickCode);
            if (m3U8Infos.Count > 0)
            {
                //选择最高分辨率的播放
                FileMatch.PlayByPotPlayer(m3U8Infos[0].Url);
            }
        }

        public async Task<List<m3u8Info>> Getm3u8InfoByPickCode(string pickCode)
        {
            List<m3u8Info> m3U8Infos = new List<m3u8Info>();

            HttpResponseMessage response;
            string strResult;
            try
            {
                response = await Client.GetAsync($"https://v.anxia.com/site/api/video/m3u8/{pickCode}.m3u8");
                strResult = await response.Content.ReadAsStringAsync();
            }
            catch
            {
                return m3U8Infos;
                //Debug.WriteLine("获取m3u8链接失败");
            }

            var lineList = strResult.Split(new char[] { '\n' });
            for (int i = 0; i < lineList.Count(); i++)
            {
                var lineText = lineList[i].Trim('\r');

                var re = Regex.Match(lineText, @"BANDWIDTH=(\d*),RESOLUTION=(\w*),NAME=""(\w*)""");
                if (re.Success)
                {
                    m3U8Infos.Add(new m3u8Info(re.Groups[3].Value, re.Groups[1].Value, re.Groups[2].Value, lineList[i + 1]));
                    //Debug.WriteLine(re.Groups[0].Value);
                }
            }

            //排序
            m3U8Infos = m3U8Infos.OrderByDescending(x => x.Bandwidth).ToList();

            return m3U8Infos;
        }

        /// <summary>
        /// mpv播放
        /// </summary>
        /// <param name="playUrl"></param>
        /// <param name="FileName"></param>
        /// <param name="showWindow"></param>
        /// <param name="referrerUrl"></param>
        /// <param name="user_agnet"></param>
        public void Play115SourceVideoWithMpv(string playUrl, string FileName, bool showWindow = true, string referrerUrl = "https://115.com", string user_agnet = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/104.0.5112.81 Safari/537.36 Edg/104.0.1293.47")
        {
            var process = new Process();

            process.StartInfo.FileName = FileName;
            process.StartInfo.Arguments = @$" ""{playUrl}"" --referrer=""{referrerUrl}"" --user-agent=""{user_agnet}""";
            process.StartInfo.UseShellExecute = false;
            if (!showWindow)
            {
                process.StartInfo.CreateNoWindow = true;
            }

            process.Start();
        }

        /// <summary>
        /// vlc播放
        /// </summary>
        /// <param name="playUrl"></param>
        /// <param name="FileName"></param>
        /// <param name="showWindow"></param>
        /// <param name="referrerUrl"></param>
        /// <param name="user_agnet"></param>
        public void Play115SourceVideoWithVlc(string playUrl, string FileName, bool showWindow = true, string referrerUrl = "https://115.com", string user_agnet = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/104.0.5112.81 Safari/537.36 Edg/104.0.1293.47")
        {
            var process = new Process();

            process.StartInfo.FileName = FileName;
            process.StartInfo.Arguments = @$" ""{playUrl}"" :http-referrer=""{referrerUrl}"" :http-user-agent=""{user_agnet}""";
            process.StartInfo.UseShellExecute = false;
            if (!showWindow)
            {
                process.StartInfo.CreateNoWindow = true;
            }

            process.Start();
        }
    
        public void PlayVideoWithOriginUrl(string pickcode)
        {
            var downUrlList = GetDownUrl(pickcode);
            if (downUrlList.Count==0) return;

            string downUrl = downUrlList[0];
            switch (AppSettings.PlayerSelection)
            {
                case 2:
                    Play115SourceVideoWithMpv(downUrl,AppSettings.MpvExePath);
                    break;
                case 3:
                    Play115SourceVideoWithVlc(downUrl,AppSettings.VlcExePath);
                    break;

            }
        }
    }
}
