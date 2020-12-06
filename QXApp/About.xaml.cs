using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Windows.Storage;
using Windows.Storage.Pickers;
using Windows.Storage.Provider;
using Windows.UI.Core;
using Windows.UI.Popups;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Navigation;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.ApplicationModel;
using Windows.ApplicationModel.DataTransfer;
using SQLite.Net;
using QXScan.Core;
using Windows.UI.Xaml.Input;
 
// The Blank Page item template is documented at http://go.microsoft.com/fwlink/?LinkId=234238

namespace QXScan
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class About : Page
    {
        private SQLiteConnection session;

        private ObservableCollection<History> cache;

        public ObservableCollection<History> Cache
        {
            get
            {
                if (this.cache == null)
                {
                    var list = this.session.Table<History>().ToList();

                    this.cache = new ObservableCollection<History>(list);
                }

                return this.cache;
            }
        } 

        public About()
        {
            this.InitializeComponent();

            this.session = DbFactory.Open(App.ConnectionString); 
        }

        protected override void OnNavigatingFrom(NavigatingCancelEventArgs e)
        {
            if (this.session != null)
            {
                this.session.Close();
                this.session = null;
            }
        }

        private void Grid_Holding(object sender, HoldingRoutedEventArgs e)
        {
            FrameworkElement menu = sender as FrameworkElement;
            FlyoutBase flyoutBase = FlyoutBase.GetAttachedFlyout(menu);

            flyoutBase.ShowAt(menu);
        }

        private async void Copy_Click(object sender, RoutedEventArgs e)
        {
            var data = (e.OriginalSource as FrameworkElement).DataContext as History;

            await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
            {
                DataPackage obj = new DataPackage { RequestedOperation = DataPackageOperation.Copy };

                obj.SetText(data.Text);

                Clipboard.SetContent(obj);
            });
        }

        private async void Delete_Click(object sender, RoutedEventArgs e)
        {
            var data = (e.OriginalSource as FrameworkElement).DataContext as History;

            var dialog = new MessageDialog(data.Text);

            dialog.Title = "Delete the selected?";

            dialog.Commands.Add(new UICommand("Yes") { Id = 1 });
            dialog.Commands.Add(new UICommand("No") { Id = 0 });

            var result = await dialog.ShowAsync();

            var action = (int)result.Id;

            if (action == 1)
            {
                try
                {
                    this.session.Delete(data);

                    this.Cache.Remove(data); 
                }
                catch (Exception ex)
                {
                    await App.Logger.Write(ex.Message);
                }
            }
        }

        private async void AppBarSave_Click(object sender, RoutedEventArgs e)
        {
            var savePicker = new FileSavePicker();

            savePicker.SuggestedStartLocation = PickerLocationId.DocumentsLibrary;

            savePicker.FileTypeChoices.Add("Excel CSV", new List<string>() { ".csv" });

            // Default file name if the user does not type one in or select a file to replace
            savePicker.SuggestedFileName = "New Document";

            StorageFile file = await savePicker.PickSaveFileAsync();

            if (file == null)
                return;

            try
            {
                CachedFileManager.DeferUpdates(file);

                using (var stream = await file.OpenAsync(FileAccessMode.ReadWrite))
                {
                    var dw = new Windows.Storage.Streams.DataWriter(stream);

                    foreach (var p in this.Cache)
                    {
                        dw.WriteString(p.Text + ", " + p.CreateDate.ToString() + "\r\n");
                    }

                    await dw.StoreAsync();
                    await stream.FlushAsync();
                }

                FileUpdateStatus status = await CachedFileManager.CompleteUpdatesAsync(file);

                string message = "Save the data to " + file.Name + "  successfully.";

                if (status != FileUpdateStatus.Complete)
                {
                    message = "File " + file.Name + " couldn't be saved.";
                }

                var dialog = new MessageDialog(message);

                dialog.Commands.Add(new UICommand("OK") { Id = 0 });

                await dialog.ShowAsync();
            }
            catch (Exception ex)
            {
                await App.Logger.Write(ex.Message);
            }            
        }

        private async void barDelete_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new MessageDialog("Are you sure to delete the history?");

            dialog.Commands.Add(new UICommand("Yes") { Id = 1 });
            dialog.Commands.Add(new UICommand("No") { Id = 0 });

            var result = await dialog.ShowAsync();

            if ((int)result.Id == 1)
            {
                try
                {
                    this.session.DeleteAll<History>();

                    this.Cache.Clear();
                }
                catch (Exception ex)
                {
                    await App.Logger.Write(ex.Message);
                }
            }
        }

        private void barScan_Click(object sender, RoutedEventArgs e)
        {
            this.Frame.Navigate(typeof(MainPage));
        }
   
        private async void Pivot_PivotItemLoading(Pivot sender, PivotItemEventArgs args)
        {
            var header = args.Item.Header.ToString();

            if (header == "History")
            {
                await this.Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () => 
                {
                    this.hlist.ItemsSource = this.Cache;
                });
                
            }
            else if (header == "About")
            { 
                PackageId packageId = Package.Current.Id;
                PackageVersion ver = packageId.Version;

                await this.Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
                {
                    this.txtNo.Text = ver.Major + "." + ver.Minor + "." + ver.Build;
                });
            }        
        }
 
        private async void Grid_Tapped(object sender, TappedRoutedEventArgs e)
        {
            var data = (e.OriginalSource as FrameworkElement).DataContext as History;

            if (data != null)
            {
                var dialog = new MessageDialog(data.Text);

                dialog.Title = "Detail";

                if (StringHelper.IsURL(data.Text))
                {
                    dialog.Commands.Add(new UICommand("Open") { Id = 2 });
                }
               
                dialog.Commands.Add(new UICommand("Close") { Id = 1 });

                var result = await dialog.ShowAsync();

                var action = (int)result.Id;

                if (action == 2)
                {
                    await Windows.System.Launcher.LaunchUriAsync(new Uri(data.Text));
                }

                this.hlist.SelectedIndex = -1;
            }
        }
    }
}
