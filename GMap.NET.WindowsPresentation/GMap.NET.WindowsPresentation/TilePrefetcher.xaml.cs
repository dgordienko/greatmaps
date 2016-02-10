
 // ReSharper disable once CheckNamespace
namespace GMap.NET.WindowsPresentation
{
   using System;
   using System.Collections.Generic;
   using System.ComponentModel;
   using System.Diagnostics.CodeAnalysis;
   using System.Windows;
   using System.Windows.Input;
   using GMap.NET.Internals;
   using GMap.NET;
   using GMap.NET.MapProviders;
   using System.Threading;
   using System.Windows.Threading;

   /// <summary>
   /// form helping to prefetch tiles on local db
   /// </summary>
   // ReSharper disable once UnusedMember.Global
   [SuppressMessage("ReSharper", "ArrangeThisQualifier")]
   [SuppressMessage("ReSharper", "MemberCanBePrivate.Global")]
   [SuppressMessage("ReSharper", "DelegateSubtraction")]
   public partial class TilePrefetcher
   {
       private readonly BackgroundWorker worker = new BackgroundWorker();

       private List<GPoint> list = new List<GPoint>();

       private int zoom;

       private GMapProvider provider;

       private int sleep;

       private int all;

       public bool ShowCompleteMessage = false;

       private RectLatLng area;

       private GSize maxOfTiles;

      public TilePrefetcher()
      {
         InitializeComponent();

         GMaps.Instance.OnTileCacheComplete += OnTileCacheComplete;
         GMaps.Instance.OnTileCacheStart += OnTileCacheStart;
         GMaps.Instance.OnTileCacheProgress += OnTileCacheProgress;

         worker.WorkerReportsProgress = true;
         worker.WorkerSupportsCancellation = true;
         worker.ProgressChanged += WorkerProgressChanged;
         worker.DoWork += WorkerDoWork;
         worker.RunWorkerCompleted += WorkerRunWorkerCompleted;
      }

       private readonly AutoResetEvent done = new AutoResetEvent(true);

       private void OnTileCacheComplete()
      {
           if (!IsVisible) return;
            done.Set();

            Dispatcher.Invoke(DispatcherPriority.Normal, new Action(() =>
               {
                   label2.Text = "all tiles saved";
               }));
      }

       private void OnTileCacheStart()
      {
          if (!IsVisible) return;
            done.Reset();

            Dispatcher.Invoke(DispatcherPriority.Normal, new Action(() =>
              {
                  label2.Text = "saving tiles...";
              }));
      }

      void OnTileCacheProgress(int left)
      {
         if(IsVisible)
         {
            Dispatcher.Invoke(DispatcherPriority.Normal, new Action(() =>
            {
               label2.Text = left + " tile to save...";
            }));
         }
      }

      public void Start(RectLatLng a, int z, GMapProvider p, int s)
      {
          if (worker.IsBusy) return;
            label1.Text = "...";
            progressBar1.Value = 0;

            area = a;
            zoom = z;
            provider = p;
            sleep = s;

          GMaps.Instance.UseMemoryCache = false;
          GMaps.Instance.CacheOnIdleRead = false;
          GMaps.Instance.BoostCacheEngine = true;

            worker.RunWorkerAsync();

            ShowDialog();
      }

      volatile bool stopped;

      public void Stop()
      {
         GMaps.Instance.OnTileCacheComplete -= OnTileCacheComplete;
         GMaps.Instance.OnTileCacheStart -= OnTileCacheStart;
         GMaps.Instance.OnTileCacheProgress -= OnTileCacheProgress;

         done.Set();

         if(worker.IsBusy)
         {
            worker.CancelAsync();
         }

         GMaps.Instance.CancelTileCaching();

         stopped = true;

         done.Close();
      }

       private void WorkerRunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
      {
         if(ShowCompleteMessage)
         {
            if(!e.Cancelled)
            {
               MessageBox.Show("Prefetch Complete! => " + ((int)e.Result).ToString() + " of " + all);
            }
            else
            {
               MessageBox.Show("Prefetch Canceled! => " + ((int)e.Result).ToString() + " of " + all);
            }
         }

         list.Clear();

         GMaps.Instance.UseMemoryCache = true;
         GMaps.Instance.CacheOnIdleRead = true;
         GMaps.Instance.BoostCacheEngine = false;

            Close();
      }

       private bool CacheTiles(int z, GPoint p)
      {
         foreach(var type in provider.Overlays)
         {
            Exception ex;
            PureImage img;

            // tile number inversion(BottomLeft -> TopLeft) for pergo maps
            if(type is TurkeyMapProvider)
            {
               img = GMaps.Instance.GetImageFrom(type, new GPoint(p.X, maxOfTiles.Height - p.Y), z, out ex);
            }
            else // ok
            {
               img = GMaps.Instance.GetImageFrom(type, p, z, out ex);
            }

            if(img != null)
            {
               img.Dispose();
            }
            else
            {
               return false;
            }
         }
         return true;
      }

       public void WorkerDoWork(object sender, DoWorkEventArgs e)
      {
         if(list != null)
         {
            list.Clear();
            list = null;
         }
         list = provider.Projection.GetAreaTileList(area, zoom, 0);
         maxOfTiles = provider.Projection.GetTileMatrixMaxXY(zoom);
         all = list.Count;

         var countOk = 0;
         var retry = 0;

         Stuff.Shuffle(list);

         for(var i = 0; i < all; i++)
         {
            if(worker.CancellationPending)
               break;

            var p = list[i];
            {
               if(CacheTiles(zoom, p))
               {
                  countOk++;
                  retry = 0;
               }
               else
               {
                   if(++retry <= 1) // retry only one
                  {
                     i--;
                     Thread.Sleep(1111);
                     continue;
                  }
                   retry = 0;
               }
            }

            worker.ReportProgress((i + 1) * 100 / all, i + 1);

            Thread.Sleep(sleep);
         }

         e.Result = countOk;

         if(!stopped)
         {
            done.WaitOne();
         }
      }

       private void WorkerProgressChanged(object sender, ProgressChangedEventArgs e)
      {
            label1.Text = "Fetching tile at zoom (" + zoom + "): " + (int)e.UserState + " of " 
                + all + ", complete: " + e.ProgressPercentage + "%";
            progressBar1.Value = e.ProgressPercentage;
      }

      protected override void OnPreviewKeyDown(KeyEventArgs e)
      {
         if(e.Key == Key.Escape)
         {
                Close();
         }

         base.OnPreviewKeyDown(e);
      }

      protected override void OnClosed(EventArgs e)
      {
            Stop();

         base.OnClosed(e);
      }
   }
}
