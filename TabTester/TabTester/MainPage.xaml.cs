﻿//using Microsoft.Toolkit.Uwp.UI.Controls;
using System;
using Microsoft.UI.Xaml.Controls;
using System.Collections.ObjectModel;
using System.Linq;
using Windows.ApplicationModel.Core;
using Windows.ApplicationModel.DataTransfer;
using Windows.UI.WindowManagement;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Hosting;
using Windows.UI.Xaml.Navigation;
using Windows.UI.Xaml.Media;
using Windows.UI.ViewManagement;
using Microsoft.UI.Xaml.Controls.Primitives;

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

            // TODO: This is not yet implemented in WinUI
            //Tabs.TabItems.VectorChanged += Items_VectorChanged;
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
                for (int i = 0; i < 5; i++)
                {
                    // TODO: Add a user control to the content of the TabViewItem
                    Tabs.TabItems.Add(new TabViewItem() { IconSource = new Microsoft.UI.Xaml.Controls.SymbolIconSource() { Symbol = Symbol.Placeholder }, Header = $"Item {i}", Content = new MyTabContentControl() { DataContext = $"Item {i}" } });
                }

                Tabs.SelectedIndex = 0;

                // Extend into the titlebar
                var coreTitleBar = CoreApplication.GetCurrentView().TitleBar;
                coreTitleBar.ExtendViewIntoTitleBar = true;

                coreTitleBar.LayoutMetricsChanged += CoreTitleBar_LayoutMetricsChanged;

                var titleBar = ApplicationView.GetForCurrentView().TitleBar;
                titleBar.ButtonBackgroundColor = Windows.UI.Colors.Transparent;
                titleBar.ButtonForegroundColor = Windows.UI.Colors.Gray;

                Window.Current.SetTitleBar(CustomDragRegion);
            }
            else
            {
                // Secondary AppWindows --- keep track of the window
                RootAppWindow = window;

                // Extend into the titlebar
                window.TitleBar.ExtendsContentIntoTitleBar = true;
                window.TitleBar.ButtonBackgroundColor = Windows.UI.Colors.Transparent;
                window.TitleBar.ButtonForegroundColor = Windows.UI.Colors.Gray;

                // TODO: What we really want is: CustomDragRegion.MinWidth = titlebBar.SystemOverlayRightInset;
                //window.Changed += Window_Changed;

                CustomDragRegion.MinWidth = 188;

                window.Frame.DragRegionVisuals.Add(CustomDragRegion);
            }
        }

        //private void Window_Changed(AppWindow sender, AppWindowChangedEventArgs args)
        //{
        //    if (args.DidTitleBarChange)
        //    {
        //        var occ = sender.TitleBar.GetTitleBarOcclusions();
        //        var occ1 = occ[0];
        //        var rect1 = occ1.OccludingRect;
        //    }
        //}

        private void CoreTitleBar_LayoutMetricsChanged(CoreApplicationViewTitleBar sender, object args)
        {
            CustomDragRegion.MinWidth = sender.SystemOverlayRightInset;
        }

        public void AddTabToTabs(TabViewItem tab)
        {
            Tabs.TabItems.Add(tab);
        }


        // Create a new Window once the Tab is dragged outside.
        private async void Tabs_TabDroppedOutside(TabView sender, TabViewTabDroppedOutsideEventArgs args)
        {
            AppWindow newWindow = await AppWindow.TryCreateAsync();

            var newPage = new MainPage();
            newPage.SetupWindow(newWindow);

            ElementCompositionPreview.SetAppWindowContent(newWindow, newPage);

            Tabs.TabItems.Remove(args.Tab);
            newPage.AddTabToTabs(args.Tab);

            // TODO: Remove when TabView.TabItems.VectorChagned is exposed.
            sender.Tag = newWindow;

            await newWindow.TryShowAsync();
        }

        private void Tabs_TabDragStarting(TabView sender, TabViewTabDragStartingEventArgs args)
        {
            // TODO: This crashes when updating to WinUI because e.Items[0] is NULL. Why?

            // TODO: Why does e.Items[0] return the ToggleSwitch and not the item itself??

            // We can only drag one tab at a time, so grab the first one...
            var firstItem = args.Tab;

            // ... set the drag data to the tab...
            args.Data.Properties.Add(DataIdentifier, firstItem);

            // ... and indicate that we can move it 
            args.Data.RequestedOperation = DataPackageOperation.Move;
        }

        private async void Tabs_TabStripDrop(object sender, DragEventArgs e)
        {
            // This event is called when we're dragging between different TabViews
            // It is responsible for handling the drop of the item into the second TabView

            object obj;
            if (e.DataView.Properties.TryGetValue(DataIdentifier, out obj))
            {
                // TODO - BUG: obj should never be null, but occassionally is. Why?
                if (obj == null)
                {
                    return;
                }

                var destinationTabView = sender as TabView;
                var destinationItems = destinationTabView.TabItems;

                if (destinationItems != null)
                {
                    // First we need to get the position in the List to drop to
                    var index = -1;

                    // Determine which items in the list our pointer is inbetween.
                    for (int i = 0; i < destinationTabView.TabItems.Count; i++)
                    {
                        var item = destinationTabView.ContainerFromIndex(i) as TabViewItem;

                        if (e.GetPosition(item).X - item.ActualWidth < 0)
                        {
                            index = i;
                            break;
                        }
                    }

                    // The TabView can only be in one tree at a time. Before moving it to the new TabView, remove it from the old.
                    var destinationTabViewListView = ((obj as TabViewItem).Parent as TabViewListView);
                    destinationTabViewListView.Items.Remove(obj);

                    if (index < 0)
                    {
                        // We didn't find a transition point, so we're at the end of the list
                        destinationItems.Add(obj);
                    }
                    else if (index < destinationTabView.TabItems.Count)
                    {
                        // Otherwise, insert at the provided index.
                        destinationItems.Insert(index, obj);
                    }

                    // Select the newly dragged tab
                    destinationTabView.SelectedItem = obj;

                    // TODO: This logic should be handled by VectorChanged, but since VectorChanged isn't yet impl, handle it here instead
                    if (destinationTabViewListView.Items.Count == 0 && destinationTabView.Tag != null)
                    {
                        await (destinationTabView.Tag as AppWindow).CloseAsync();
                    }
                }
            }
        }

        // This method prevents the TabView from handling things that aren't text (ie. files, images, etc.)
        private void Tabs_TabStripDragOver(object sender, DragEventArgs e)
        {
            if (e.DataView.Properties.ContainsKey(DataIdentifier))
            {
                e.AcceptedOperation = DataPackageOperation.Move;
            }
        }


        private void Tabs_AddTabButtonClick(TabView sender, object args)
        {
            sender.TabItems.Add(new TabViewItem() { IconSource = new Microsoft.UI.Xaml.Controls.SymbolIconSource() { Symbol = Symbol.Placeholder }, Header = "New Item", Content = new MyTabContentControl() { DataContext = "New Item" } });
        }

        Windows.UI.Color stashedColor;

        private void BackgroundColorAppBarButton_Click(object sender, RoutedEventArgs e)
        {
            // TODO: Tabs.Background doesn't set the correct thing yet. This chunk of code is a big workaround and will be removed.
            var contains = Application.Current.Resources.ContainsKey("TabViewBackground");

            bool isPlain = (sender as AppBarToggleButton).IsChecked == true;

            if (contains)
            {
                var resource = Application.Current.Resources["TabViewBackground"];

                if (isPlain)
                {
                    stashedColor = (resource as SolidColorBrush).Color;

                    (resource as SolidColorBrush).Color = GetAccentColorForTheme();
                }
                else
                {
                    (resource as SolidColorBrush).Color = stashedColor;
                }
            }
        }

        private Windows.UI.Color GetAccentColorForTheme()
        {
            if (IsEffectivelyLightTheme())
            {
                return (Windows.UI.Color)Application.Current.Resources["SystemAccentColorLight2"];
            }
            else
            {
                return (Windows.UI.Color)Application.Current.Resources["SystemAccentColorDark2"];
            }
        }

        private void ThemeAppBarButton_Click(object sender, RoutedEventArgs e)
        {
            RootPage.RequestedTheme = IsEffectivelyLightTheme() ? ElementTheme.Dark : ElementTheme.Light;
        }

        private bool IsEffectivelyLightTheme()
        {
            bool isLight = false;

            if (RootPage.RequestedTheme == ElementTheme.Light)
            {
                isLight = true;
            }
            else if (RootPage.RequestedTheme == ElementTheme.Dark)
            {
                isLight = false;
            }
            else
            {
                // Theme = default
                if (((SolidColorBrush)Application.Current.Resources["ApplicationPageBackgroundThemeBrush"]).Color == Windows.UI.Colors.White)
                {
                    isLight = true;
                }
                else
                {
                    isLight = false;
                }

            }

            return isLight;
        }

        private void AcrylicAppBarToggleButton_Click(object sender, RoutedEventArgs e)
        {
            bool isPlain = (sender as AppBarToggleButton).IsChecked == true;

            if (isPlain)
            {
                Tabs.Background = (Brush)Application.Current.Resources["SystemControlAcrylicWindowBrush"];
            }
            else
            {
                Tabs.Background = null;
            }
        }

        private void WidthAppBarButton_Click(object sender, RoutedEventArgs e)
        {
            Tabs.TabWidthMode = Tabs.TabWidthMode == TabViewWidthMode.Equal ? TabViewWidthMode.SizeToContent : TabViewWidthMode.Equal;
        }

        private void Tabs_TabCloseRequested(TabView sender, TabViewTabCloseRequestedEventArgs args)
        {
            sender.TabItems.Remove(args.Tab);
        }
    }
}
