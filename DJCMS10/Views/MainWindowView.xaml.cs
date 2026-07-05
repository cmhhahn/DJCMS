using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Controls.Primitives;
using DJCMS.Models;
using DJCMS.ViewModels;

namespace DJCMS.Views
{
    public partial class MainWindowView : Window
    {
        private Point _dragStartPoint;
        private ListBoxItem? _draggedItem;
        private bool _dropAfter;
        private bool _isFullScreen = false;
        private WindowState _previousWindowState;
        private WindowStyle _previousWindowStyle;
        private double _previousMaxHeight;
        private double _previousMaxWidth;

        public MainWindowView()
        {
            InitializeComponent();
            SourceInitialized += MainWindowView_SourceInitialized;
            StateChanged += MainWindowView_StateChanged;
        }

        // Resize thumb handlers
        private void ResizeLeft_DragDelta(object sender, DragDeltaEventArgs e)
        {
            if (WindowState == WindowState.Maximized) return;

            double newWidth = Width - e.HorizontalChange;
            double minW = MinWidth > 0 ? MinWidth : 200;

            if (newWidth >= minW)
            {
                Left += e.HorizontalChange;
                Width = newWidth;
            }
            else
            {
                double delta = Width - minW;
                Left += delta;
                Width = minW;
            }
        }

        private void ResizeRight_DragDelta(object sender, DragDeltaEventArgs e)
        {
            if (WindowState == WindowState.Maximized) return;

            double newWidth = Width + e.HorizontalChange;
            double minW = MinWidth > 0 ? MinWidth : 200;

            if (newWidth >= minW)
                Width = newWidth;
            else
                Width = minW;
        }

        private void ResizeTop_DragDelta(object sender, DragDeltaEventArgs e)
        {
            if (WindowState == WindowState.Maximized) return;

            double newHeight = Height - e.VerticalChange;
            double minH = MinHeight > 0 ? MinHeight : 150;

            if (newHeight >= minH)
            {
                Top += e.VerticalChange;
                Height = newHeight;
            }
            else
            {
                double delta = Height - minH;
                Top += delta;
                Height = minH;
            }
        }

        private void ResizeBottom_DragDelta(object sender, DragDeltaEventArgs e)
        {
            if (WindowState == WindowState.Maximized) return;

            double newHeight = Height + e.VerticalChange;
            double minH = MinHeight > 0 ? MinHeight : 150;

            if (newHeight >= minH)
                Height = newHeight;
            else
                Height = minH;
        }

        private void ResizeTopLeft_DragDelta(object sender, DragDeltaEventArgs e)
        {
            ResizeLeft_DragDelta(sender, e);
            ResizeTop_DragDelta(sender, e);
        }

        private void ResizeTopRight_DragDelta(object sender, DragDeltaEventArgs e)
        {
            ResizeRight_DragDelta(sender, e);
            ResizeTop_DragDelta(sender, e);
        }

        private void ResizeBottomLeft_DragDelta(object sender, DragDeltaEventArgs e)
        {
            ResizeLeft_DragDelta(sender, e);
            ResizeBottom_DragDelta(sender, e);
        }

        private void ResizeBottomRight_DragDelta(object sender, DragDeltaEventArgs e)
        {
            ResizeRight_DragDelta(sender, e);
            ResizeBottom_DragDelta(sender, e);
        }

        private void MainWindowView_SourceInitialized(object? sender, EventArgs e)
        {
            // Ensure window respects the working area when maximized
            MaxHeight = SystemParameters.MaximizedPrimaryScreenHeight;
            MaxWidth = SystemParameters.MaximizedPrimaryScreenWidth;
        }

        private void MainWindowView_StateChanged(object? sender, EventArgs e)
        {
            // Don't adjust border when in full-screen mode
            if (_isFullScreen)
                return;

            // Remove the border thickness when maximized to prevent gaps
            if (WindowState == WindowState.Maximized)
            {
                // Find the border element and adjust
                if (Content is Border border)
                {
                    border.BorderThickness = new Thickness(0);
                    border.Padding = new Thickness(7); // Add padding to compensate for hidden border
                }
            }
            else
            {
                // Restore the border when not maximized
                if (Content is Border border)
                {
                    border.BorderThickness = new Thickness(1);
                    border.Padding = new Thickness(0);
                }
            }
        }

        // Window control button handlers
        private void MinimizeButton_Click(object sender, RoutedEventArgs e)
        {
            WindowState = WindowState.Minimized;
        }

        private void MaximizeButton_Click(object sender, RoutedEventArgs e)
        {
            // Exit full-screen if currently in full-screen mode
            if (_isFullScreen)
            {
                ExitFullScreen();
            }

            WindowState = WindowState == WindowState.Maximized
                ? WindowState.Normal
                : WindowState.Maximized;
        }

        private void FullScreenButton_Click(object sender, RoutedEventArgs e)
        {
            if (_isFullScreen)
            {
                ExitFullScreen();
            }
            else
            {
                EnterFullScreen();
            }
        }

        private void MenuButton_Click(object sender, RoutedEventArgs e)
        {
            // Toggle the custom popup menu instead of using the system context menu
            if (MenuPopup != null)
            {
                MenuPopup.IsOpen = !MenuPopup.IsOpen;
            }
        }

        private void EnterFullScreen()
        {
            // Store current state
            _previousWindowState = WindowState;
            _previousMaxHeight = MaxHeight;
            _previousMaxWidth = MaxWidth;
            _isFullScreen = true;

            // Remove size restrictions for true full-screen
            MaxHeight = double.PositiveInfinity;
            MaxWidth = double.PositiveInfinity;

            // If already maximized, temporarily set to Normal to trigger the state change
            if (WindowState == WindowState.Maximized)
            {
                WindowState = WindowState.Normal;
            }

            // Maximize to cover entire screen including taskbar
            WindowState = WindowState.Maximized;

        }

        private void ExitFullScreen()
        {
            _isFullScreen = false;

            // Restore size restrictions
            MaxHeight = _previousMaxHeight;
            MaxWidth = _previousMaxWidth;

            // Restore window state
            WindowState = _previousWindowState;


        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ClickCount == 2)
            {
                MaximizeButton_Click(sender, e);
            }
            else
            {
                DragMove();
            }
        }

        private void ListBoxItem_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            _dragStartPoint = e.GetPosition(null);
        }

        private void ListBoxItem_PreviewMouseMove(object sender, MouseEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed && sender is ListBoxItem listBoxItem)
            {
                Point currentPosition = e.GetPosition(null);
                Vector diff = _dragStartPoint - currentPosition;

                if (Math.Abs(diff.X) > SystemParameters.MinimumHorizontalDragDistance ||
                    Math.Abs(diff.Y) > SystemParameters.MinimumVerticalDragDistance)
                {
                    var track = listBoxItem.DataContext as PlaylistTrack;

                    if (track != null)
                    {
                        _draggedItem = listBoxItem;
                        listBoxItem.Tag = "Dragging";

                        DragDrop.DoDragDrop(listBoxItem, track, DragDropEffects.Move);

                        listBoxItem.Tag = null;
                        _draggedItem = null;
                    }
                }
            }
        }

        private void ListBoxItem_DragOver(object sender, DragEventArgs e)
        {
            bool hasValidData = false;
            DragDropEffects effect = DragDropEffects.None;

            if (sender is ListBoxItem targetItem && targetItem != _draggedItem)
            {
                // Check for internal track reordering
                if (e.Data.GetData(typeof(PlaylistTrack)) is PlaylistTrack)
                {
                    hasValidData = true;
                    effect = DragDropEffects.Move;
                }
                // Check for external file drops
                else if (e.Data.GetDataPresent(DataFormats.FileDrop))
                {
                    hasValidData = true;
                    effect = DragDropEffects.Copy;
                }

                if (hasValidData)
                {
                    Point position = e.GetPosition(targetItem);
                    double middleY = targetItem.ActualHeight / 2;

                    if (position.Y < middleY)
                    {
                        targetItem.Tag = "DragOverTop";
                        _dropAfter = false;
                    }
                    else
                    {
                        targetItem.Tag = "DragOverBottom";
                        _dropAfter = true;
                    }
                }
            }
            // Allow dropping on empty space for external files
            else if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                effect = DragDropEffects.Copy;
            }

            e.Effects = effect;
            e.Handled = true;
        }

        private void ListBoxItem_DragLeave(object sender, DragEventArgs e)
        {
            if (sender is ListBoxItem targetItem)
            {
                targetItem.Tag = null;
            }
        }

        private void ListBoxItem_Drop(object sender, DragEventArgs e)
        {
            if (sender is ListBoxItem targetItem)
            {
                targetItem.Tag = null;
            }

            var droppedTrack = e.Data.GetData(typeof(PlaylistTrack)) as PlaylistTrack;

            if (droppedTrack != null && Library.Items.Contains(droppedTrack) && DataContext is MainWindowViewModel vm)
            {
                var files = new string[] { droppedTrack.FilePath };
                HandleDroppedFiles(files, sender, vm);
                return;
            }

            // Handle internal track reordering
            if (droppedTrack !=null && sender is ListBoxItem targetListBoxItem &&
                targetListBoxItem.DataContext is PlaylistTrack targetTrack &&
                DataContext is MainWindowViewModel viewModel)
            {
                int droppedIndex = viewModel.Tracks.IndexOf(droppedTrack);
                int targetIndex = viewModel.Tracks.IndexOf(targetTrack);

                if (droppedIndex >= 0 && targetIndex >= 0)
                {
                    int newIndex = targetIndex;

                    if (_dropAfter)
                    {
                        newIndex = targetIndex;
                        if (droppedIndex < targetIndex)
                        {
                            newIndex = targetIndex;
                        }
                        else
                        {
                            newIndex = targetIndex + 1;
                        }
                    }
                    else
                    {
                        newIndex = targetIndex;
                        if (droppedIndex > targetIndex)
                        {
                            newIndex = targetIndex;
                        }
                        else
                        {
                            newIndex = targetIndex - 1;
                        }
                    }

                    if (droppedIndex != newIndex && newIndex >= 0 && newIndex < viewModel.Tracks.Count)
                    {
                        viewModel.Tracks.Move(droppedIndex, newIndex);
                    }
                }
            }
            // Handle external file drops
            else if (e.Data.GetDataPresent(DataFormats.FileDrop) && DataContext is MainWindowViewModel vm2)
            {
                var files = (string[])e.Data.GetData(DataFormats.FileDrop);

                HandleDroppedFiles(files, sender, vm2);
            }

            e.Handled = true;
        }

        private void HandleDroppedFiles(string[] files, object sender, MainWindowViewModel vm)
        {
            if (sender is ListBoxItem dropTargetItem && dropTargetItem.DataContext is PlaylistTrack dropTargetTrack)
            {
                // Insert at the position of the target item
                int targetIndex = vm.Tracks.IndexOf(dropTargetTrack);
                if (!_dropAfter)
                {
                    // Drop before the target
                    vm.LoadFilesAtIndex(files, targetIndex);
                }
                else
                {
                    // Drop after the target
                    vm.LoadFilesAtIndex(files, targetIndex + 1);
                }
            }
            else
            {
                // Drop at the end if no specific target
                vm.LoadFiles(files);
            }
        }

        private void ListBox_DragOver(object sender, DragEventArgs e)
        {
            // Allow dropping external files on empty space in the ListBox
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                e.Effects = DragDropEffects.Copy;
            }
            else
            {
                e.Effects = DragDropEffects.None;
            }

            e.Handled = true;
        }

        private void ListBox_Drop(object sender, DragEventArgs e)
        {
            // Handle external file drops on empty space
            if (e.Data.GetDataPresent(DataFormats.FileDrop) && DataContext is MainWindowViewModel viewModel)
            {
                var files = (string[])e.Data.GetData(DataFormats.FileDrop);
                viewModel.LoadFiles(files);
            }

            e.Handled = true;
        }

        private void UIElement_OnPreviewMouseMove(object sender, MouseEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed)
            {
                // If in full-screen, exit and continue dragging
                if (_isFullScreen || WindowState == WindowState.Maximized)
                {
                    // Store the mouse position relative to the screen before exiting full-screen
                    Point mouseScreenPos = PointToScreen(e.GetPosition(this));

                    MaximizeButton_Click(null, null);

                    // Calculate the new window position so the mouse stays under the title bar
                    // Center the window under the cursor
                    double newLeft = mouseScreenPos.X - (Width / 2);
                    double newTop = mouseScreenPos.Y - 20; // Approximate title bar height

                    // Update window position
                    Left = newLeft;
                    Top = newTop;

                    DragMove();
                }
            }
        }

        private void Splitter_DragDelta(object sender, DragDeltaEventArgs e)
        {

        }
    }
}
