using Microsoft.Toolkit.Uwp.UI.Controls;
using System;
using System.Collections.ObjectModel;
using System.Linq;
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
        AppWindow RootAppWindow = null;

        private const string DataIdentifier = "MyTabItem";

        public MainPage()
        {
            this.InitializeComponent();

            Tabs.Items.VectorChanged += Items_VectorChanged;
        }

        private async void Items_VectorChanged(Windows.Foundation.Collections.IObservableVector<object> sender, Windows.Foundation.Collections.IVectorChangedEventArgs @event)
        {

            // If there are no more tabs and we're in a secondary AppWindow, close that Window.
            if (sender.Count == 0 && RootAppWindow != null)
            {
                await RootAppWindow.CloseAsync();
            }

            // TODO: Close the root CoreApplicationView?
        }

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
                    // TODO: Add a user control to the content of the TabViewItem
                    Tabs.Items.Add(new TabViewItem() { Icon = new SymbolIcon() { Symbol = Symbol.Placeholder }, Header = $"Item {i}", Content = new ToggleSwitch() { Header=$"Item {i}" } });
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

        public void AddTabToTabs(TabViewItem tab)
        {
            Tabs.Items.Add(tab);
        }

        // Create a new Window once the Tab is dragged outside.
        private async void TabView_TabDraggedOutside(object sender, Microsoft.Toolkit.Uwp.UI.Controls.TabDraggedOutsideEventArgs e)
        {
            AppWindow newWindow = await AppWindow.TryCreateAsync();

            var newPage = new MainPage();
            newPage.SetupWindow(newWindow);

            ElementCompositionPreview.SetAppWindowContent(newWindow, newPage);

            Tabs.Items.Remove(e.Tab);
            newPage.AddTabToTabs(e.Tab);

            await newWindow.TryShowAsync();
        }


        private void Tab_OnDragItemsStarting(object sender, DragItemsStartingEventArgs e)
        {
            // TODO: Why does e.Items[0] return the ToggleSwitch and not the item itself??

            // We can only drag one tab at a time, so grab the first one...
            var firstItem = (e.Items[0] as FrameworkElement).Parent; 

            // ... set the drag data to the tab...
            e.Data.Properties.Add(DataIdentifier, firstItem);

            // ... and indicate that we can move it 
            e.Data.RequestedOperation = DataPackageOperation.Move;
        }

        private void Tab_Drop(object sender, DragEventArgs e)
        {
            // This event is called when we're dragging between different TabViews
            // It is responsible for handling the drop of the item into the second TabView

            object obj;
            if (e.DataView.Properties.TryGetValue(DataIdentifier, out obj))
            {
                var destinationTabView = sender as TabView;
                var destinationItems = destinationTabView.Items;

                if (destinationItems != null)
                {
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

                    // The TabView can only be in one tree at a time. Before moving it to the new TabView, remove it from the old.
                    ((obj as TabViewItem).Parent as TabView).Items.Remove(obj);


                    if (index < 0)
                    {
                        // We didn't find a transition point, so we're at the end of the list
                        destinationItems.Add(obj);
                    }
                    else if (index < destinationTabView.Items.Count)
                    {
                        // Otherwise, insert at the provided index.
                        destinationItems.Insert(index, obj);
                    }

                    // Select the newly dragged tab
                    destinationTabView.SelectedItem = obj;
                }
            }
        }

        // This method prevents the TabView from handling things that aren't text (ie. files, images, etc.)
        private void Tab_DragOver(object sender, DragEventArgs e)
        {
            if (e.DataView.Properties.ContainsKey(DataIdentifier))
            {
                e.AcceptedOperation = DataPackageOperation.Move;
            }
        }
    }
}
