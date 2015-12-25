using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;


using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using System.Text;
using Windows.Storage;
using Windows.Storage.Streams;
using Windows.Storage.Pickers;
using Windows.UI.Xaml.Shapes;
using Windows.UI;
using Windows.ApplicationModel.Background;


namespace Downloader
{
    public sealed partial class MainPage : Page
    {
        public MainPage()
        {

            this.InitializeComponent();
            DownloadPathBox.Text = resourceAddress;
        }
        class DownloadInfo
        {
            public DownloadInfo(string t) { tempfilename = t; }
            public string tempfilename;
            public StorageFile file;
            public Task a;
            public double rate;
        };
        List<DownloadInfo> m_downloads;

        long m_initialTime;
        bool m_cancelled = false; int m_completed = 0;
        long m_totalsize;
        string m_ext;

        string resourceAddress = "https://www.python.org/ftp/python/2.7.8/python-2.7.8.msi";
        // Problem with : http://androidnetworktester.googlecode.com/files/1mb.txt
        //https://www.python.org/ftp/python/2.7.8/python-2.7.8.msi
        //imgs.xkcd.com/comics/im_so_random.png
        //download.microsoft.com/download/A/3/8/A38FFBF2-1122-48B4-AF60-E44F6DC28BD8/ENUS/amd64/MSEInstall.exe
        //speed.mirror.sptel.com.au/1mb.dat
        async void Start()
        {

            m_initialTime = DateTime.Now.Ticks;
            {
                HttpClient client = new HttpClient();
                HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Get, resourceAddress);
                HttpResponseMessage response;
                try
                {
                    response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead);
                    if (!response.IsSuccessStatusCode) throw new Exception();

                    if (response.Headers.AcceptRanges.ElementAt(0)!="none")
                    {
                        m_downloads = new List<DownloadInfo> { 
                                new DownloadInfo("test1"),
                                new DownloadInfo("test2"),
                                new DownloadInfo("test3"),
                                new DownloadInfo("test4"),
                                new DownloadInfo("test5"),
                                new DownloadInfo("test6"),
                                new DownloadInfo("test7"),
                                new DownloadInfo("test8"),
                                //new DownloadInfo("test9")
                        };
                    }
                    else
                    {
                        m_downloads = new List<DownloadInfo> { new DownloadInfo("test1") };
                    }
                }
                catch {
                    MarshalLog("Error: Can't get response");
                    return;
                }
               
                m_totalsize = response.Content.Headers.ContentLength.Value;
                m_ext = System.IO.Path.GetExtension(resourceAddress);
            }

            ProgressView.Maximum = m_totalsize;
            long sz = m_totalsize / m_downloads.Count;
            m_CancellationSource = new CancellationTokenSource();
            for (int i = 0; i < m_downloads.Count; ++i)
            {
                m_downloads[i].rate = 0;
                m_downloads[i].file = await ApplicationData.Current.LocalFolder.CreateFileAsync(m_downloads[i].tempfilename, CreationCollisionOption.ReplaceExisting);
               
                var pview = new ProgressBar(); pview.Height = 15;
                var TextView = new TextBlock();TextView.FontSize = 16;
                SegmentsView.Children.Add(pview);
                SegmentsView.Children.Add(TextView);

                if (i == m_downloads.Count - 1)
                    m_downloads[i].a = StartDownload(m_downloads[i], i * sz, null, pview, TextView);
                else
                    m_downloads[i].a = StartDownload(m_downloads[i], i * sz, i * sz + sz - 1, pview, TextView);
            }
            for (int i = 0; i < m_downloads.Count; ++i)
                await m_downloads[i].a;

            if (m_completed == m_downloads.Count)
            {
                await JoinFiles();
                long dt = DateTime.Now.Ticks - m_initialTime;
                MarshalLog("Total Time Taken: " + new TimeSpan(dt).ToString(@"hh\:mm\:ss"));
                for (int i = 0; i < m_downloads.Count; ++i)
                    await m_downloads[i].file.DeleteAsync(StorageDeleteOption.PermanentDelete);
            }

        }

        async Task JoinFiles()
        {
            FileSavePicker savePicker = new FileSavePicker();
            savePicker.SuggestedStartLocation = PickerLocationId.Desktop;
            savePicker.FileTypeChoices.Add("File", new List<string>() { m_ext });
            var File0 = await savePicker.PickSaveFileAsync();
            if (File0 != null)
            {
                var access = await File0.OpenAsync(FileAccessMode.ReadWrite);
                var outstream = access.GetOutputStreamAt(0);
                var writer = new DataWriter(outstream);
                ProgressView.Value = 0;
                ProgressView.Foreground = new SolidColorBrush(Colors.Blue);
                ProgressView.Maximum *= 2 + 100;
                for (int i = 0; i < m_downloads.Count; ++i)
                {
                    IRandomAccessStream readStream;
                lbl0:
                    try {
                        readStream = await m_downloads[i].file.OpenAsync(FileAccessMode.Read);
                    }
                    catch {
                        goto lbl0;
                    }

                    IInputStream inputSteam = readStream.GetInputStreamAt(0);
                    DataReader dataReader = new DataReader(inputSteam);
                    uint numBytesLoaded = await dataReader.LoadAsync((uint)readStream.Size);
                    var bytes = new byte[numBytesLoaded];
                    dataReader.ReadBytes(bytes);
                    //dataReader.Dispose();
                    ProgressView.Value += numBytesLoaded;

                    writer.WriteBytes(bytes);
                    ProgressView.Value += bytes.Length;

                    ProgressView.Value += numBytesLoaded;
                }
                await writer.StoreAsync();
                await outstream.FlushAsync();
            }
            ProgressView.Value = ProgressView.Maximum; 
        }

        private void CancelDownloadClick(object sender, RoutedEventArgs e)
        {
            if (!m_cancelled && m_CancellationSource!=null)
            {
                m_CancellationSource.Cancel();
                m_cancelled = true;
            }
        }

        void MarshalLog(string str)
        {
            LogView.Text = str;
        }

        void DrawLineChart(List<Point> points)
        {
            GraphView.Children.Clear();
            Polyline pline = new Polyline();
            pline.StrokeThickness = 2;
            pline.Stroke = new SolidColorBrush(Colors.Cyan);

            PointCollection pointc = new PointCollection();
            foreach (var x in points)
                pointc.Add(x);
            pline.Points = pointc;
            GraphView.Children.Add(pline);
        }

        Point Normalize(Point pt, double xmax, double ymax)
        {
            Point npt = new Point();

            npt.X = pt.X * GraphView.ActualWidth / xmax;
            npt.Y = GraphView.ActualHeight - pt.Y * GraphView.ActualHeight / ymax;

            return npt;
        }

        List<Point> m_points = new List<Point>();
        long ticks = DateTime.Now.Ticks, m_pbytes = 0; double avgrate = 0;
        long m_downloadedSize = 0;
        void UpdateView()
        {
            long nTicks = DateTime.Now.Ticks;
            long dt = nTicks - ticks;
            if (dt > TimeSpan.TicksPerSecond / 5)
            {
                avgrate = (m_downloadedSize - m_pbytes) / (double)dt * TimeSpan.TicksPerSecond;
                if (avgrate < 1024)
                    SpeedView.Text = avgrate.ToString() + " bytes per second";
                else
                    SpeedView.Text = (avgrate / 1024).ToString() + " KB per second";


                m_points.Add(Normalize(new Point(m_downloadedSize, avgrate), m_totalsize, 800*1024));
                DrawLineChart(m_points);

                avgrate = 0;
                ticks = nTicks;
                m_pbytes = m_downloadedSize;
            }
            InfoView.Text = m_downloadedSize.ToString() + " bytes out of " + m_totalsize.ToString() + " bytes downloaded";
            ProgressView.Value = m_downloadedSize;
        }
       
        CancellationTokenSource m_CancellationSource = null;
        private async Task StartDownload(DownloadInfo downloadInfo, long? from, long? to, ProgressBar pbar, TextBlock tblock)
        {
            try
            {           
                var client = new System.Net.Http.HttpClient();
                client.DefaultRequestHeaders.Range = new System.Net.Http.Headers.RangeHeaderValue(from, to);
                var uri = new Uri(resourceAddress);
                HttpResponseMessage responseMessage = await client.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead, m_CancellationSource.Token);
                long? contentLength = responseMessage.Content.Headers.ContentLength;


                long ticks, newticks, dt;
                using (Stream fileStream = await downloadInfo.file.OpenStreamForWriteAsync())
                {
                    int totalNumberOfBytesRead = 0;
                    using (var responseStream = await responseMessage.Content.ReadAsStreamAsync())
                    {
                        int numberOfReadBytes;
                        do
                        {
                            ticks = DateTime.Now.Ticks;
                            if (m_CancellationSource.Token.IsCancellationRequested)
                                m_CancellationSource.Token.ThrowIfCancellationRequested();

                            const int bufferSize = 1024*1024;
                            byte[] responseBuffer = new byte[bufferSize];
                            numberOfReadBytes = await responseStream.ReadAsync(responseBuffer, 0, responseBuffer.Length);
                            totalNumberOfBytesRead += numberOfReadBytes;

                            fileStream.Write(responseBuffer, 0, numberOfReadBytes);

                            m_downloadedSize += numberOfReadBytes;
                            newticks = DateTime.Now.Ticks;
                            dt = newticks - ticks;
                            if (dt > 100000)
                            {
                                ticks = newticks;
                                downloadInfo.rate = numberOfReadBytes / (double)dt * TimeSpan.TicksPerSecond;
                                if (downloadInfo.rate < 1024)
                                    tblock.Text = downloadInfo.rate.ToString() + " bytes per second";
                                else
                                    tblock.Text = (downloadInfo.rate / 1024).ToString() + " KB per second";
                                UpdateView();
                            }
                            if (contentLength.HasValue)
                            {
                                //double progressPercent = totalNumberOfBytesRead / (double)contentLength * 100;
                                pbar.Maximum = (double)contentLength;
                                pbar.Value = totalNumberOfBytesRead;
                            }
                            else
                            {
                                // Just display the read bytes   
                            }
                        } while (numberOfReadBytes != 0);
                    }
                }
                m_completed++;
            }
            catch (HttpRequestException hre)
            {
                MarshalLog("ERROR \r\n");
                MarshalLog("HttpRequestException: " + hre.ToString());
            }
            catch (OperationCanceledException)
            {
                MarshalLog("CANCELLED \r\n");
            }
            //catch(Exception ex)
            //{
            //    MarshalLog("ERROR \r\n");
            //    MarshalLog("Exception: " + ex.ToString());
            //}
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            
        }

        private void Button_Click_1(object sender, RoutedEventArgs e)
        {
            resourceAddress = DownloadPathBox.Text;
            StartView.Visibility = Windows.UI.Xaml.Visibility.Collapsed;
            DownloadView.Visibility = Windows.UI.Xaml.Visibility.Visible;
            Start();
        }







        //
        // Register a background task with the specified taskEntryPoint, name, trigger,
        // and condition (optional).
        //
        // taskEntryPoint: Task entry point for the background task.
        // taskName: A name for the background task.
        // trigger: The trigger for the background task.
        // condition: Optional parameter. A conditional event that must be true for the task to fire.
        //
        public static BackgroundTaskRegistration RegisterBackgroundTask(string taskEntryPoint,
                                                                        string taskName,
                                                                        IBackgroundTrigger trigger,
                                                                        IBackgroundCondition condition)
        {

            foreach (var cur in BackgroundTaskRegistration.AllTasks)
                if (cur.Value.Name == taskName)
                    return (BackgroundTaskRegistration)(cur.Value);

            var builder = new BackgroundTaskBuilder();

            builder.Name = taskName;
            builder.TaskEntryPoint = taskEntryPoint;
            builder.SetTrigger(trigger);

            if (condition != null)
                builder.AddCondition(condition);

            BackgroundTaskRegistration task = builder.Register();
            return task;
        }
    }
}


