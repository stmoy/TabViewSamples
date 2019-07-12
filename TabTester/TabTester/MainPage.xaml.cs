using Microsoft.Toolkit.Uwp.UI.Controls;
using System;
using System.Collections.ObjectModel;
using Windows.ApplicationModel.Core;
using Windows.ApplicationModel.DataTransfer;
using Windows.UI.WindowManagement;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Hosting;
using Windows.UI.Xaml.Navigation;

namespace TabTester
{
    // https://docs.microsoft.com/en-us/windows/uwp/design/shell/title-bar
    public sealed partial class MainPage : Page
    {
        // TODO: Support more than just strings...
        ObservableCollection<string> MyItems = new ObservableCollection<string>();

        AppWindow RootAppWindow = null;
        public MainPage()
        {
            this.InitializeComponent();

            MyItems.CollectionChanged += MyItems_CollectionChanged;
        }

        private async void MyItems_CollectionChanged(object sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        {
            // If there are no more tabs and we're in a secondary AppWindow, close that Window.
            if ((sender as ObservableCollection<string>).Count == 0 && RootAppWindow != null)
            {
                await RootAppWindow.CloseAsync();
            }
            
            // TODO: Close the root CoreApplicationView?
        }

        // TODO: BUG?: OnNavigatedTo is never called in the AppWindow codepath?
        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);

            SetupWindow(null);
        }

        void SetupWindow(AppWindow window)
        {
            if (window == null)
            {
                // Main Window -- add some default items
                for (int i = 0; i < 10; i++)
                {
                    MyItems.Add("Item " + i);
                }

                // Extend into the titlebar
                var coreTitleBar = CoreApplication.GetCurrentView().TitleBar;
                coreTitleBar.ExtendViewIntoTitleBar = true;

                Window.Current.SetTitleBar(CustomDragRegion);
            }
            else
            {
                // Secondary AppWindows --- keep track of the window
                RootAppWindow = window;

                // Extend into the titlebar
                window.TitleBar.ExtendsContentIntoTitleBar = true;
                window.TitleBar.ButtonBackgroundColor = Windows.UI.Colors.Transparent;

                window.Frame.DragRegionVisuals.Add(CustomDragRegion);
            }
        }

        public void AddItemToItems(object item)
        {
            MyItems.Add(item.ToString());
        }

        // Create a new Window once the Tab is dragged outside.
        private async void TabView_TabDraggedOutside(object sender, Microsoft.Toolkit.Uwp.UI.Controls.TabDraggedOutsideEventArgs e)
        {
            AppWindow newWindow = await AppWindow.TryCreateAsync();

            var newPage = new MainPage();
            newPage.SetupWindow(newWindow);

            ElementCompositionPreview.SetAppWindowContent(newWindow, newPage);

            newPage.AddItemToItems(e.Item);

            await newWindow.TryShowAsync();
        }


        private void Tab_OnDragItemsStarting(object sender, DragItemsStartingEventArgs e)
        {
            // We can only drag one tab at a time, so grab the first one...
            var firstItem = e.Items[0];

            // ... set the drag data to the text of the tab...
            e.Data.SetText(firstItem.ToString());

            // ... and indicate that we can move it 
            e.Data.RequestedOperation = DataPackageOperation.Move;
        }

        private async void Tab_Drop(object sender, DragEventArgs e)
        {
            // This event is called when we're dragging between different TabViews
            // It is responsible for handling the drop of the item into the second TabView
            if (e.DataView.Contains(StandardDataFormats.Text))
            {
                var destinationTabView = sender as TabView;
                var destinationItemsSource = destinationTabView?.ItemsSource as ObservableCollection<string>;

                if (destinationItemsSource != null)
                {
                    var text = await e.DataView.GetTextAsync();

                    // First we need to get the position in the List to drop to
                    var index = -1;

                    // Determine which items in the list our pointer is inbetween.
                    for (int i = 0; i < destinationTabView.Items.Count; i++)
                    {
                        var item = destinationTabView.ContainerFromIndex(i) as TabViewItem;

                        if (e.GetPosition(item).X - item.ActualWidth < 0)
                        {
                            index = i;
                            break;
                        }
                    }

                    if (index < 0)
                    {
                        // We didn't find a transition point, so we're at the end of the list
                        destinationItemsSource.Add(text);
                    }
                    else if (index < destinationTabView.Items.Count)
                    {
                        // Otherwise, insert at the provided index.
                        destinationItemsSource.Insert(index, text);
                    }
                }
            }
        }

        // This method prevents the TabView from handling things that aren't text (ie. files, images, etc.)
        // TODO: Is this method necessary?
        private void Tab_DragOver(object sender, DragEventArgs e)
        {
            if (e.DataView.Contains(StandardDataFormats.Text))
            {
                e.AcceptedOperation = DataPackageOperation.Move;
            }
        }

        // Remove the item from the collection once the drag has been completed.
        private void Tab_DragItemsCompleted(ListViewBase sender, DragItemsCompletedEventArgs args)
        {
            // TODO: When reorganizing tabs within the same window, the tab gets removed.
            // We need to maintain state somehow, but not on MainPage (because we create a new MainPage every time)
            
            var item = args.Items[0] as string;
            var tabViewItemSource = sender?.ItemsSource as ObservableCollection<string>;
            tabViewItemSource.Remove(item);
        }
    }
}
