﻿using Data;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;
using Windows.Foundation;
using Windows.Foundation.Collections;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace Display.Control
{
    public sealed partial class Explorer : UserControl
    {
        ObservableCollection<ExplorerItem> TreeViewDataSource;
        ObservableCollection<ExplorerItem> SelectFolderName;
        ObservableCollection<FilesInfo> FileInSelectFolder;

        //存储获取过的Datum，避免重复获取
        List<StoreDatum> StoreDataList = new();

        public Explorer()
        {
            this.InitializeComponent();

            TreeViewDataSource = GetRootFolder();
            foreach (var item in TreeViewDataSource)
            {
                TreeViewNode Node = new TreeViewNode()
                {
                    Content = item,
                    HasUnrealizedChildren = item.HasUnrealizedChildren,
                    IsExpanded = item.IsExpanded
                };
                FolderTreeView.RootNodes.Add(Node);
                //FillTreeNode(Node);
            }

            FileInSelectFolder = new ObservableCollection<FilesInfo>();
            SelectFolderName = new ObservableCollection<ExplorerItem>();

            tryUpdataFolderInfo("0");
        }


        //获取Cid所有的文件（文件和文件夹）
        public List<Datum> GetFilesFromItems(string folderCid, FilesInfo.FileType outType)
        {
            List<Datum> items;

            //先从存储的List中获取
            var item = StoreDataList.Where(x => x.Cid == folderCid).FirstOrDefault();
            if (item == null)
            {
                items = DataAccess.GetListByCid(folderCid);
                StoreDataList.Add(new StoreDatum()
                {
                    Cid = folderCid,
                    DatumList = items
                });
            }
            else
            {
                items = item.DatumList;
            }

            if(outType == FilesInfo.FileType.Folder)
            {

                var itamsdfs = items.Where(x => string.IsNullOrEmpty(x.fid));
                items = items.Where(x => string.IsNullOrEmpty(x.fid)).ToList();
            }

            return items;
        }

        //获取根目录下的文件夹
        public ObservableCollection<ExplorerItem> GetRootFolder()
        {
            var list = new ObservableCollection<ExplorerItem>();

            //位于根目录下的文件夹
            var data = GetFilesFromItems("0", FilesInfo.FileType.Folder);
            //var data = DataAccess.GetFolderListByPid("0");

            foreach (var item in data)
            {
                //var nextFolders = DataAccess.GetFolderListByPid(item.cid);

                //ObservableCollection<ExplorerItem> children = new();
                bool hasUnrealizedChildren = DataAccess.GetFolderListByPid(item.cid, 1).Count != 0;

                ExplorerItem Folders = new ExplorerItem()
                {
                    Name = item.n,
                    Type = FilesInfo.FileType.Folder,
                    Cid = item.cid,
                    HasUnrealizedChildren = hasUnrealizedChildren,
                };

                list.Add(Folders);
            }

            return list;
        }

        /// <summary>
        /// 点击了TreeView选项
        /// </summary>
        private string _lastInvokedCid = "";
        private void TreeView_ItemInvoked(TreeView sender, TreeViewItemInvokedEventArgs args)
        {
            var content = ((args.InvokedItem as TreeViewNode).Content as ExplorerItem);
            var cid = content.Cid;

            //避免重复点击
            if (cid == _lastInvokedCid) return;
            else _lastInvokedCid = cid;

            tryUpdataFolderInfo(cid);
        }

        /// <summary>
        /// 根据cid更新右侧的信息（显示目录 和 文件详情）
        /// </summary>
        /// <param name="folderCid"></param>
        private void tryUpdataFolderInfo(string folderCid)
        {
            var items = GetFilesFromItems(folderCid, FilesInfo.FileType.File);

            //更新右侧文件夹目录
            SelectFolderName.Clear();
            tryUpdateFolder(folderCid);

            //更新右侧文件列表
            tryUpdateFileInSelectFolder(items);

        }

        /// <summary>
        /// 更新详细信息的目录
        /// </summary>
        /// <param name="folderCid"></param>
        private void tryUpdateFolder(string folderCid)
        {
            if (folderCid == "0")
            {
                SelectFolderName.Clear();
                SelectFolderName.Add(new ExplorerItem()
                {
                    Name = "根目录",
                    Cid = "0"

                });
            }
            else
            {
                ////从getNodeByRootNodeWithCid中获取根目录的Node
                //TreeViewNode node = getNodeByRootNodeWithCid(FolderTreeView.RootNodes.ToList(), folderCid);

                //List<ExplorerItem> dirToRootNameList = new();

                //while (node != null && node.Content is ExplorerItem)
                //{
                //    var item = node.Content as ExplorerItem;
                //    dirToRootNameList.Add(item);
                //    node = node.Parent;
                //}

                //dirToRootNameList.Reverse();


                //foreach (var info in dirToRootNameList)
                //{
                //    SelectFolderName.Add(new ExplorerItem()
                //    {
                //        Name = info.Name,
                //        Cid = info.Cid

                //    });
                //}

                //从数据库中获取根目录信息
                List<Datum> FolderToRootList = DataAccess.getRootByCid(folderCid);
                foreach(var info in FolderToRootList)
                {
                    SelectFolderName.Add(new ExplorerItem()
                    {
                        Name = info.n,
                        Cid = info.cid
                    });
                }

            }

        }

        /// <summary>
        /// 通过文件Cid获取根目录的Node
        /// </summary>
        /// <param name="rootNodes"></param>
        /// <param name="folderCid"></param>
        /// <returns></returns>
        private TreeViewNode getNodeByRootNodeWithCid(List<TreeViewNode> rootNodes, string folderCid)
        {
            //不存在Cid未零的Node
            if (folderCid == "0") return null;

            TreeViewNode targertNode = null;

            List<TreeViewNode> Nodechildrens = rootNodes;


            while (targertNode == null && Nodechildrens.Count != 0)
            {
                List<TreeViewNode> tmpChildrenNode = new();
                foreach (TreeViewNode node in Nodechildrens)
                {
                    //Content
                    if (((ExplorerItem)node.Content).Cid == folderCid)
                    {
                        targertNode = node;
                        break;
                    }
                    //Children
                    else
                    {
                        foreach (var cnode in node.Children)
                        {
                            tmpChildrenNode.Add(cnode);
                        }
                    }
                }
                Nodechildrens = tmpChildrenNode;
            }

            return targertNode;
        }

        /// <summary>
        /// 更新所选文件夹的文件列表
        /// </summary>
        /// <param name="items"></param>
        private void tryUpdateFileInSelectFolder(List<Datum> items)
        {
            FileInSelectFolder.Clear();

            //排序
            items = items.OrderByDescending(x => x.pid).ToList();

            foreach (var file in items)
            {
                //FilesInfo.FileType fileType = file.pid == "" ? FilesInfo.FileType.File : FilesInfo.FileType.Folder;
                FileInSelectFolder.Add(new FilesInfo(file));
            }

            var item = FileInSelectFolder;
            //FileInSelectFolder.Clear();
        }

        /// <summary>
        /// TreeView 展开
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="args"></param>
        private void TreeView_Expanding(TreeView sender, TreeViewExpandingEventArgs args)
        {
            //标记为 内含未加载项
            if (args.Node.HasUnrealizedChildren)
            {
                bool isNeedLoad = true;

                if(_markShowPartFolderItemList.Count > 0)
                {
                    var currentCid = (args.Node.Content as ExplorerItem).Cid;
                    foreach (var item in _markShowPartFolderItemList)
                    {
                        //找到之前未加载完成的记录，
                        //即之前加载过了，无需重复加载
                        if ((item.InsertNode.Content as ExplorerItem).Cid == currentCid)
                        {
                            ShowNumTextBlock.Visibility = Visibility.Visible;
                            ShowNumTip.Text = $"{item.ShowNum}/{item.LastFolderItem.Count}";
                            _lastFolderItemList = item;
                            isNeedLoad = false;
                            break;
                        }
                    }
                }

                if (isNeedLoad)
                {
                    FillTreeNode(args.Node);
                }
            }
        }

        private List<lastUnAllShowFolderItem> _markShowPartFolderItemList = new();
        private lastUnAllShowFolderItem _lastFolderItemList;
        /// <summary>
        /// 填充之前TreeView未加载的子节点
        /// </summary>
        /// <param name="node"></param>
        /// <param name="MaxNum"></param>
        private void FillTreeNode(TreeViewNode node, int MaxNum = 30, bool isInsertLeft = false)
        {
            // Get the contents of the folder represented by the current tree node.
            // Add each item as a new child node of the node that's being expanded.

            // Only process the node if it's a folder and has unrealized children.
            ExplorerItem folder;

            if (node.Content is ExplorerItem)
            {
                folder = node.Content as ExplorerItem;
            }
            else
            {
                // The node isn't a folder, or it's already been filled.
                return;
            }

            List<Datum> itemsList;

            if (isInsertLeft && _lastFolderItemList !=null)
            {
                itemsList = _lastFolderItemList.LastFolderItem;
                itemsList = itemsList.GetRange(MaxNum, MaxNum);
                _markShowPartFolderItemList.Remove(_lastFolderItemList);
                _lastFolderItemList = null;
            }
            else
            {
                itemsList = GetFilesFromItems(folder.Cid, FilesInfo.FileType.Folder);
                //itemsList = DataAccess.GetFolderListByPid(folder.Cid);
            }

            if (itemsList.Count == 0)
            {
                // The item is a folder, but it's empty. Leave HasUnrealizedChildren = true so
                // that the chevron appears, but don't try to process children that aren't there.
                return;
            }

            List<Datum> itemspartList;
            bool hasUnrealizedChildren = false;

            // 显示部分
            if (itemsList.Count > MaxNum)
            {
                ShowNumTextBlock.Visibility = Visibility.Visible;
                ShowNumTip.Text = $"{MaxNum}/{itemsList.Count}";
                itemspartList = itemsList.GetRange(0, MaxNum);
                _lastFolderItemList = new lastUnAllShowFolderItem()
                {
                    InsertNode = node,
                    LastFolderItem = itemsList,
                    Count = itemsList.Count,
                    ShowNum = MaxNum
                };
                _markShowPartFolderItemList.Add(_lastFolderItemList);
                hasUnrealizedChildren = true;
            }
            // 数量较小，直接显示全部
            else
            {
                itemspartList = itemsList;
                ShowNumTextBlock.Visibility = Visibility.Collapsed;
            }

            startUpdateTreeView(node, itemspartList);

            //标记 下一级是否有未加载的
            node.HasUnrealizedChildren = hasUnrealizedChildren;
        }

        private async void startUpdateTreeView(TreeViewNode node,List<Datum> itemsList)
        {
            readFileProgressBar.Maximum = itemsList.Count;
            readFileProgressBar.Value = 0;
            readFileProgressBar.Visibility = Visibility.Visible;

            var progress = new Progress<int>(progressPercent => readFileProgressBar.Value = progressPercent);

            ////排序
            //itemsList = itemsList.OrderBy(x => x.pid).ToList();

            //效率低
            Dictionary<Datum, bool> newNode_Children_Dict = await Task.Run(() => getNewNode(itemsList, progress));

            foreach (var newNodeDict in newNode_Children_Dict)
            {
                var videoInfo = newNodeDict.Key;
                var newNode = new TreeViewNode()
                {
                    Content = new ExplorerItem()
                    {
                        Name = videoInfo.n,
                        Type = FilesInfo.FileType.Folder,
                        Cid = videoInfo.cid,
                    },
                    HasUnrealizedChildren = newNodeDict.Value,
                };
                node.Children.Add(newNode);
            }

            //await Task.Run(() => {
            //    var newdfNode = new TreeViewNode();
            //});


            readFileProgressBar.Value = 0;
            readFileProgressBar.Visibility = Visibility.Collapsed;
        }

        //private List<TreeViewNode> createTreeViewNode()
        //{
        //    var result = new List<TreeViewNode>();
        //    for (int i = 0; i < 10; i++)
        //    {
        //        var node = new TreeViewNode();
        //        result.Add(node);
        //    }

        //    return result;
        //}

        /// <summary>
        /// 放入 花时 较长的 查询目录是否有下级目录的操作
        /// </summary>
        /// <param name="itemsList"></param>
        /// <param name="progress"></param>
        /// <returns></returns>
        private Dictionary<Datum, bool> getNewNode(List<Datum> itemsList, IProgress<int> progress)
        {
            var Node_HasUnrealizedChildren_Dict = new Dictionary<Datum, bool>();
            var NodeList = new List<TreeViewNode>();
            int i = 0;
            foreach (var folderInfo in itemsList)
            {
                i++;
                progress.Report(i);
                //检查下级是否还有文件夹
                bool hasUnrealizedChildren = DataAccess.GetFolderListByPid(folderInfo.cid, 1).Count != 0;

                Node_HasUnrealizedChildren_Dict.Add(folderInfo, hasUnrealizedChildren);

                //var newNode = new TreeViewNode()
                //{
                //    Content = new ExplorerItem()
                //    {
                //        Name = folderInfo.n,
                //        Type = ExplorerItem.ExplorerItemType.Folder,
                //        Cid = folderInfo.cid,
                //    },
                //    HasUnrealizedChildren = hasUnrealizedChildren,
                //};

                //newNode.HasUnrealizedChildren = hasUnrealizedChildren;

                //NodeList.Add(newNode);
            }

            return Node_HasUnrealizedChildren_Dict;
        }

        /// <summary>
        /// 全选
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void CheckBox_Checked(object sender, RoutedEventArgs e)
        {
            FolderTreeView.SelectAll();
        }

        /// <summary>
        /// 清空选项
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void CheckBox_Unchecked(object sender, RoutedEventArgs e)
        {

            FolderTreeView.SelectedNodes.Clear();
        }

        /// <summary>
        /// 点击显示目录跳转指定位置
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="args"></param>
        private void FolderBreadcrumbBar_ItemClicked(BreadcrumbBar sender, BreadcrumbBarItemClickedEventArgs args)
        {
            // Don't process last index (current location)
            if (args.Index < SelectFolderName.Count - 1)
            {
                // Home is special case.
                if (args.Index == 0)
                {
                    tryUpdataFolderInfo("0");
                }
                // Go back to the clicked item.
                else
                {
                    var item = (ExplorerItem)args.Item;

                    //var data = DataAccess.GetListByCid(item.Cid);
                    //FileInSelectFolder.Clear();
                    //foreach (var file_info in data)
                    //{
                    //    FileInSelectFolder.Add(file_info);
                    //}

                    // Remove breadcrumbs at the end until 
                    // you get to the one that was clicked.
                    while (SelectFolderName.Count > args.Index + 1)
                    {
                        SelectFolderName.RemoveAt(SelectFolderName.Count - 1);
                        tryUpdataFolderInfo(item.Cid);
                    }
                }
            }
        }

        private void Hyperlink_Click(Microsoft.UI.Xaml.Documents.Hyperlink sender, Microsoft.UI.Xaml.Documents.HyperlinkClickEventArgs args)
        {
            if (_lastFolderItemList == null) return;
            FillTreeNode(_lastFolderItemList.InsertNode,isInsertLeft:true);

            //成功
            ShowNumTextBlock.Visibility = Visibility.Collapsed;
        }

        //点击了详情页的列表
        private void FilsInfoListView_ItemClick(object sender, ItemClickEventArgs e)
        {
            var itemInfo = e.ClickedItem as FilesInfo;

            tryUpdataFolderInfo(itemInfo.Cid);
        }
    }

    public class lastUnAllShowFolderItem
    {
        public TreeViewNode InsertNode { get; set; }
        public List<Datum> LastFolderItem { get; set; }
        public int Count { get; set; }
        public int ShowNum { get; set; }
}

    /// <summary>
    /// TreeView样式选择
    /// </summary>
    public class ExplorerItemTemplateSelector : DataTemplateSelector
    {
        public DataTemplate FolderTemplate { get; set; }
        public DataTemplate FileTemplate { get; set; }

        //官方代码
        protected override DataTemplate SelectTemplateCore(object item)
        {
            var explorerItem = (ExplorerItem)(item as TreeViewNode).Content;
            if (explorerItem.Type == FilesInfo.FileType.Folder) return FolderTemplate;

            return FileTemplate;
        }
    }

    /// <summary>
    /// ListView样式选择
    /// </summary>
    public class FileInfoItemTemplateSelector : DataTemplateSelector
    {
        public DataTemplate FolderTemplate { get; set; }
        public DataTemplate FileTemplate { get; set; }


        //参照微软TreeView代码编写，实际使用中样式选择有误
        //protected override DataTemplate SelectTemplateCore(object item, DependencyObject container)
        //{
        //    var explorerItem = (FilesInfo)item;
        //    if (explorerItem.Type == FilesInfo.FileType.Folder) return FolderTemplate;

        //    return FileTemplate;
        //}


        protected override DataTemplate SelectTemplateCore(object item, DependencyObject container)
        {
            var explorerItem = (FilesInfo)item;
            if (explorerItem.Type == FilesInfo.FileType.Folder) return FolderTemplate;

            return FileTemplate;
        }
    }

}
