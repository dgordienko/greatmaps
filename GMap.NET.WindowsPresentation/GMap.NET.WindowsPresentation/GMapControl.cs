
 // ReSharper disable once CheckNamespace
namespace GMap.NET.WindowsPresentation
{
   using System;
   using System.Collections.Generic;
   using System.Collections.ObjectModel;
   using System.ComponentModel;
   using System.Globalization;
   using System.Linq;
   using System.Windows;
   using System.Windows.Controls;
   using System.Windows.Data;
   using System.Windows.Input;
   using System.Windows.Media;
   using System.Windows.Media.Effects;
   using System.Windows.Media.Imaging;
   using System.Windows.Shapes;
   using System.Windows.Threading;
   using GMap.NET;
   using GMap.NET.Internals;
   using System.Diagnostics;
   using System.Diagnostics.CodeAnalysis;
   using GMap.NET.MapProviders;
   using GMap.NET.Projections;
   using static System.Int32;

    /// <summary>
   /// GMap.NET control for Windows Presentation
   /// </summary>
   [SuppressMessage("ReSharper", "MemberCanBePrivate.Global")]
   [SuppressMessage("ReSharper", "FieldCanBeMadeReadOnly.Global")]
   [SuppressMessage("ReSharper", "ArrangeThisQualifier")]
   [SuppressMessage("ReSharper", "MemberCanBeProtected.Global")]
   [SuppressMessage("ReSharper", "CompareOfFloatsByEqualityOperator")]
   [SuppressMessage("ReSharper", "UnusedMember.Local")]
   [SuppressMessage("ReSharper", "PossibleLossOfFraction")]
   [SuppressMessage("ReSharper", "UnusedMember.Global")]
   [SuppressMessage("ReSharper", "VirtualMemberNeverOverriden.Global")]
    public class GMapControl : ItemsControl, Interface, IDisposable
   {
      #region DependencyProperties and related stuff

      public Point MapPoint
      {
         get
         {
            return (Point)GetValue(MapPointProperty);
         }
         set
         {
            SetValue(MapPointProperty, value);
         }
      }


      // Using a DependencyProperty as the backing store for point.  This enables animation, styling, binding, etc...
      public static readonly DependencyProperty MapPointProperty =
             DependencyProperty.Register("MapPoint", typeof(Point), typeof(GMapControl), new PropertyMetadata(new Point(), OnMapPointPropertyChanged));


      private static void OnMapPointPropertyChanged(DependencyObject source,
      DependencyPropertyChangedEventArgs e)
      {
          var temp = (Point)e.NewValue;
          var gMapControl = source as GMapControl;
          if (gMapControl != null) gMapControl.Position = new PointLatLng(temp.X, temp.Y);
      }

        public static readonly DependencyProperty MapProviderProperty = DependencyProperty.Register("MapProvider", typeof(GMapProvider), typeof(GMapControl), 
            new UIPropertyMetadata(EmptyProvider.Instance, MapProviderPropertyChanged));

      /// <summary>
      /// type of map
      /// </summary>
      [Browsable(false)]
      public GMapProvider MapProvider
      {
         get
         {
            return GetValue(MapProviderProperty) as GMapProvider;
         }
         set
         {
            SetValue(MapProviderProperty, value);
         }
      }

      private static void MapProviderPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
      {
         GMapControl map = (GMapControl)d;
         if(map != null && e.NewValue != null)
         {
            Debug.WriteLine("MapType: " + e.OldValue + " -> " + e.NewValue);

            RectLatLng viewarea = map.SelectedArea;
            if(viewarea != RectLatLng.Empty)
            {
               map.Position = new PointLatLng(viewarea.Lat - viewarea.HeightLat / 2, viewarea.Lng + viewarea.WidthLng / 2);
            }
            else
            {
               viewarea = map.ViewArea;
            }

            map.core.Provider = e.NewValue as GMapProvider;

            map.copyright = null;
            if(!string.IsNullOrEmpty(map.core.Provider?.Copyright))
            {
               map.copyright = new FormattedText(map.core.Provider.Copyright, CultureInfo.CurrentUICulture, FlowDirection.LeftToRight, new Typeface("GenericSansSerif"), 9, Brushes.Navy);
            }

             if (!map.core.IsStarted || !map.core.zoomToArea) return;
             // restore zoomrect as close as possible
             if (viewarea == RectLatLng.Empty || viewarea == map.ViewArea) return;
             var bestZoom = map.core.GetMaxZoomToFitRect(viewarea);
             if(bestZoom > 0 && map.Zoom != bestZoom)
             {
                 map.Zoom = bestZoom;
             }
         }
      }

      public static readonly DependencyProperty ZoomProperty = DependencyProperty.Register("Zoom", typeof(double), typeof(GMapControl),
          new UIPropertyMetadata(0.0, ZoomPropertyChanged, OnCoerceZoom));

      /// <summary>
      /// map zoom
      /// </summary>
      [Category("GMap.NET")]
      public double Zoom
      {
         get
         {
            return (double)(GetValue(ZoomProperty));
         }
         set
         {
            SetValue(ZoomProperty, value);
         }
      }

      private static object OnCoerceZoom(DependencyObject o, object value)
      {
         var map = o as GMapControl;
          if (map == null) return value;
          var result = (double)value;
          if(result > map.MaxZoom)
          {
              result = map.MaxZoom;
          }
          if(result < map.MinZoom)
          {
              result = map.MinZoom;
          }

          return result;
      }

      private ScaleModes scaleMode = ScaleModes.Integer;

      /// <summary>
      /// Specifies, if a floating map scale is displayed using a 
      /// stretched, or a narrowed map.
      /// If <code>ScaleMode</code> is <code>ScaleDown</code>,
      /// then a scale of 12.3 is displayed using a map zoom level of 13
      /// resized to the lower level. If the parameter is <code>ScaleUp</code> ,
      /// then the same scale is displayed using a zoom level of 12 with an
      /// enlarged scale. If the value is <code>Dynamic</code>, then until a
      /// remainder of 0.25 <code>ScaleUp</code> is applied, for bigger
      /// remainders <code>ScaleDown</code>.
      /// </summary>
      [Category("GMap.NET")]
      [Description("map scale type")]
      public ScaleModes ScaleMode
      {
         get
         {
            return scaleMode;
         }
         set
         {
            scaleMode = value;
            InvalidateVisual();
         }
      }

      private static void ZoomPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
      {
         var map = (GMapControl)d;          
          if (map?.MapProvider?.Projection == null) return;
          var value = (double)e.NewValue;

          Debug.WriteLine("Zoom: " + e.OldValue + " -> " + value);

          var remainder = value % 1;
          if(map.ScaleMode != ScaleModes.Integer && remainder != 0 && map.ActualWidth > 0)
          {
              bool scaleDown;
              switch(map.ScaleMode)
              {
                  case ScaleModes.ScaleDown:
                      scaleDown = true;
                      break;
                  case ScaleModes.Dynamic:
                      scaleDown = remainder > 0.25;
                      break;
                  default:
                      scaleDown = false;
                      break;
              }
              if(scaleDown)
                  remainder--;
              var scaleValue = Math.Pow(2d, remainder);
              {
                  if(map.MapScaleTransform == null)
                  {
                      map.MapScaleTransform = map.lastScaleTransform;
                  }
                  map.MapScaleTransform.ScaleX = scaleValue;
                  map.MapScaleTransform.ScaleY = scaleValue;

                  map.core.scaleX = 1 / scaleValue;
                  map.core.scaleY = 1 / scaleValue;

                  map.MapScaleTransform.CenterX = map.ActualWidth / 2;
                  map.MapScaleTransform.CenterY = map.ActualHeight / 2;
              }

              map.core.Zoom = Convert.ToInt32(scaleDown ? Math.Ceiling(value) : value - remainder);
          }
          else
          {
              map.MapScaleTransform = null;
              map.core.scaleX = 1;
              map.core.scaleY = 1;
              map.core.Zoom = (int)Math.Floor(value);
          }

          if (!map.IsLoaded) return;
          map.ForceUpdateOverlays();
          map.InvalidateVisual(true);
      }

        private readonly ScaleTransform lastScaleTransform = new ScaleTransform();

      #endregion

       private readonly Core core = new Core();
      //GRect region;
       private PointLatLng selectionStart;

       private PointLatLng selectionEnd;

        private readonly Typeface tileTypeface = new Typeface("Arial");

       private bool showTileGridLines;

       private FormattedText copyright;

      /// <summary>
      /// enables filling empty tiles using lower level images
      /// </summary>
      [Browsable(false)]
      public bool FillEmptyTiles
      {
         get
         {
            return core.fillEmptyTiles;
         }
         set
         {
                core.fillEmptyTiles = value;
         }
      }

      /// <summary>
      /// max zoom
      /// </summary>         
      [Category("GMap.NET")]
      [Description("maximum zoom level of map")]
      public int MaxZoom
      {
         get
         {
            return core.maxZoom;
         }
         set
         {
                core.maxZoom = value;
         }
      }

      /// <summary>
      /// min zoom
      /// </summary>      
      [Category("GMap.NET")]
      [Description("minimum zoom level of map")]
      public int MinZoom
      {
         get
         {
            return core.minZoom;
         }
         set
         {
                core.minZoom = value;
         }
      }

      /// <summary>
      /// pen for empty tile borders
      /// </summary>
      public Pen EmptyTileBorders = new Pen(Brushes.White, 1.0);

      /// <summary>
      /// pen for Selection
      /// </summary>
      public Pen SelectionPen = new Pen(Brushes.Blue, 2.0);

      /// <summary>
      /// background of selected area
      /// </summary>
      public Brush SelectedAreaFill = new SolidColorBrush(Color.FromArgb(33, Colors.RoyalBlue.R, Colors.RoyalBlue.G, Colors.RoyalBlue.B));

      /// <summary>
      /// pen for empty tile background
      /// </summary>
      public Brush EmptytileBrush = Brushes.Navy;

      /// <summary>
      /// text on empty tiles
      /// </summary>
      public FormattedText EmptyTileText = new FormattedText("We are sorry, but we don't\nhave imagery at this zoom\n     level for this region.", CultureInfo.CurrentUICulture, FlowDirection.LeftToRight, new Typeface("Arial"), 16, Brushes.Blue);

      /// <summary>
      /// map zooming type for mouse wheel
      /// </summary>
      [Category("GMap.NET")]
      [Description("map zooming type for mouse wheel")]
      public MouseWheelZoomType MouseWheelZoomType
      {
         get
         {
            return core.MouseWheelZoomType;
         }
         set
         {
                core.MouseWheelZoomType = value;
         }
      }
      
              /// <summary>
        /// enable map zoom on mouse wheel
        /// </summary>
        [Category("GMap.NET")]
        [Description("enable map zoom on mouse wheel")]
        public bool MouseWheelZoomEnabled
        {
            get
            {
                return core.MouseWheelZoomEnabled;
            }
            set
            {
                core.MouseWheelZoomEnabled = value;
            }
        }

      /// <summary>
      /// map dragg button
      /// </summary>
      [Category("GMap.NET")]
      public MouseButton DragButton = MouseButton.Right;

      /// <summary>
      /// use circle for selection
      /// </summary>
      public bool SelectionUseCircle = false;

      /// <summary>
      /// shows tile gridlines
      /// </summary>
      [Category("GMap.NET")]
      public bool ShowTileGridLines
      {
         get
         {
            return showTileGridLines;
         }
         set
         {
            showTileGridLines = value;
            InvalidateVisual();
         }
      }

      /// <summary>
      /// retry count to get tile 
      /// </summary>
      [Browsable(false)]
      public int RetryLoadTile
      {
         get
         {
            return core.RetryLoadTile;
         }
         set
         {
                core.RetryLoadTile = value;
         }
      }

      /// <summary>
      /// how many levels of tiles are staying decompresed in memory
      /// </summary>
      [Browsable(false)]
      public int LevelsKeepInMemmory
      {
         get
         {
            return core.LevelsKeepInMemmory;
         }

         set
         {
                core.LevelsKeepInMemmory = value;
         }
      }

      /// <summary>
      /// current selected area in map
      /// </summary>
      private RectLatLng selectedArea;

      [Browsable(false)]
      public RectLatLng SelectedArea
      {
         get
         {
            return selectedArea;
         }
         set
         {
            selectedArea = value;
            InvalidateVisual();
         }
      }

      /// <summary>
      /// is touch control enabled
      /// </summary>
      public bool TouchEnabled = true;

      /// <summary>
      /// map boundaries
      /// </summary>
      public RectLatLng? BoundsOfMap = null;

      /// <summary>
      /// occurs when mouse selection is changed
      /// </summary>        
      public event SelectionChange OnSelectionChange;

      /// <summary>
      /// list of markers
      /// </summary>
      public readonly ObservableCollection<GMapMarker> Markers = new ObservableCollection<GMapMarker>();

      /// <summary>
      /// current markers overlay offset
      /// </summary>
      internal readonly TranslateTransform MapTranslateTransform = new TranslateTransform();
      internal readonly TranslateTransform MapOverlayTranslateTransform = new TranslateTransform();

      internal ScaleTransform MapScaleTransform = new ScaleTransform();

       protected bool DesignModeInConstruct => DesignerProperties.GetIsInDesignMode(this);

       private Canvas mapCanvas;

      /// <summary>
      /// markers overlay
      /// </summary>
      [SuppressMessage("ReSharper", "PossibleNullReferenceException")]
      [SuppressMessage("ReSharper", "AssignNullToNotNullAttribute")]
      internal Canvas MapCanvas
      {
         get
         {
             if (mapCanvas != null) return mapCanvas;
             if (VisualChildrenCount <= 0) return mapCanvas;
             var border = VisualTreeHelper.GetChild(this, 0) as Border;
             var items = border.Child as ItemsPresenter;
             var target = VisualTreeHelper.GetChild(items, 0);
                mapCanvas = target as Canvas;
                mapCanvas.RenderTransform = MapTranslateTransform;
             return mapCanvas;
         }
      }

       private static DataTemplate dataTemplateInstance;

       private static ItemsPanelTemplate itemsPanelTemplateInstance;

       private static Style styleInstance;

      public GMapControl()
      {
          if (DesignModeInConstruct) return;

          #region -- templates --

          #region -- xaml --
          //  <ItemsControl Name="figures">
          //    <ItemsControl.ItemTemplate>
          //        <DataTemplate>
          //            <ContentPresenter Content="{Binding Path=Shape}" />
          //        </DataTemplate>
          //    </ItemsControl.ItemTemplate>
          //    <ItemsControl.ItemsPanel>
          //        <ItemsPanelTemplate>
          //            <Canvas />
          //        </ItemsPanelTemplate>
          //    </ItemsControl.ItemsPanel>
          //    <ItemsControl.ItemContainerStyle>
          //        <Style>
          //            <Setter Property="Canvas.Left" Value="{Binding Path=LocalPositionX}"/>
          //            <Setter Property="Canvas.Top" Value="{Binding Path=LocalPositionY}"/>
          //        </Style>
          //    </ItemsControl.ItemContainerStyle>
          //</ItemsControl> 
          #endregion

          if(dataTemplateInstance == null)
          {
              dataTemplateInstance = new DataTemplate(typeof(GMapMarker));
              {
                  var fef = new FrameworkElementFactory(typeof(ContentPresenter));
                  fef.SetBinding(ContentPresenter.ContentProperty, new Binding("Shape"));
                  dataTemplateInstance.VisualTree = fef;
              }
          }
            ItemTemplate = dataTemplateInstance;

          if(itemsPanelTemplateInstance == null)
          {
              var factoryPanel = new FrameworkElementFactory(typeof(Canvas));
              {
                  factoryPanel.SetValue(Panel.IsItemsHostProperty, true);

                  itemsPanelTemplateInstance = new ItemsPanelTemplate();
                  {
                      itemsPanelTemplateInstance.VisualTree = factoryPanel;
                  }
              }
          }
            ItemsPanel = itemsPanelTemplateInstance;

          if(styleInstance == null)
          {
              styleInstance = new Style();
              {
                  styleInstance.Setters.Add(new Setter(Canvas.LeftProperty, new Binding("LocalPositionX")));
                  styleInstance.Setters.Add(new Setter(Canvas.TopProperty, new Binding("LocalPositionY")));
                  styleInstance.Setters.Add(new Setter(Panel.ZIndexProperty, new Binding("ZIndex")));
              }
          }
            ItemContainerStyle = styleInstance;
            #endregion

            ClipToBounds = true;
            SnapsToDevicePixels = true;

            core.SystemType = "WindowsPresentation";

            core.RenderMode = RenderMode.WPF;

            core.OnMapZoomChanged += ForceUpdateOverlays;
            Loaded += GMapControlLoaded;
            Dispatcher.ShutdownStarted += DispatcherShutdownStarted;
            SizeChanged += GMapControlSizeChanged;

          // by default its internal property, feel free to use your own
          if(ItemsSource == null)
          {
                ItemsSource = Markers;
          }

            core.Zoom = (int)((double)ZoomProperty.DefaultMetadata.DefaultValue);
      }

      static GMapControl()
      {
         GMapImageProxy.Enable();
#if !PocketPC
         GMaps.Instance.SQLitePing();
#endif
      }

       private void InvalidatorEngage(object sender, ProgressChangedEventArgs e)
      {
         base.InvalidateVisual();
      }

      /// <summary>
      /// enque built-in thread safe invalidation
      /// </summary>
      public new void InvalidateVisual()
      {
            core.Refresh?.Set();
      }

        /// <summary>
      /// Invalidates the rendering of the element, and forces a complete new layout
      /// pass. System.Windows.UIElement.OnRender(System.Windows.Media.DrawingContext)
      /// is called after the layout cycle is completed. If not forced enques built-in thread safe invalidation
      /// </summary>
      /// <param name="forced"></param>
      public void InvalidateVisual(bool forced)
      {
         if(forced)
         {
            lock(core.invalidationLock)
            {
                    core.lastInvalidation = DateTime.Now;
            }
            base.InvalidateVisual();
         }
         else
         {
            InvalidateVisual();
         }
      }

      protected override void OnItemsChanged(System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
      {
         base.OnItemsChanged(e);
         
         if(e.Action == System.Collections.Specialized.NotifyCollectionChangedAction.Add)
         {
             ForceUpdateOverlays(e.NewItems);
         }
         else
         {
             InvalidateVisual();
         }
      }

      /// <summary>
      /// inits core system
      /// </summary>
      /// <param name="sender"></param>
      /// <param name="e"></param>
      private void GMapControlLoaded(object sender, RoutedEventArgs e)
      {
         if(!core.IsStarted)
         {
            if(lazyEvents)
            {
               lazyEvents = false;

               if(lazySetZoomToFitRect.HasValue)
               {
                  SetZoomToFitRect(lazySetZoomToFitRect.Value);
                  lazySetZoomToFitRect = null;
               }
            }
                core.OnMapOpen().ProgressChanged += InvalidatorEngage;
            ForceUpdateOverlays();

            if(Application.Current != null)
            {
               loadedApp = Application.Current;

               loadedApp.Dispatcher.Invoke(DispatcherPriority.ApplicationIdle,
                  new Action(delegate {
                     loadedApp.SessionEnding += CurrentSessionEnding;
                  }
                  ));
            }
         }
      }

       private Application loadedApp;

       private static void CurrentSessionEnding(object sender, SessionEndingCancelEventArgs e)
      {
         GMaps.Instance.CancelTileCaching();
      }

       private void DispatcherShutdownStarted(object sender, EventArgs e)
      {
         Dispose();
      }

      /// <summary>
      /// recalculates size
      /// </summary>
      /// <param name="sender"></param>
      /// <param name="e"></param>
      private void GMapControlSizeChanged(object sender, SizeChangedEventArgs e)
      {
         var constraint = e.NewSize;

            // 50px outside control
            //region = new GRect(-50, -50, (int)constraint.Width + 100, (int)constraint.Height + 100);

            core.OnMapSizeChanged((int)constraint.Width, (int)constraint.Height);

         if(core.IsStarted)
         {
            if(IsRotated)
            {
               UpdateRotationMatrix();
            }

            ForceUpdateOverlays();
         }
      }

       private void ForceUpdateOverlays()
      {
          ForceUpdateOverlays(ItemsSource);          
      }

       private void ForceUpdateOverlays(System.Collections.IEnumerable items)
      {
         using(Dispatcher.DisableProcessing())
         {
            UpdateMarkersOffset();

            foreach(GMapMarker i in items)
            {
                if (i == null) continue;
                i.ForceUpdateLocalPosition(this);
                (i as IShapable)?.RegenerateShape(this);
            }
         }
         InvalidateVisual();
      }

      /// <summary>
      /// updates markers overlay offset
      /// </summary>
      void UpdateMarkersOffset()
      {
         if(MapCanvas != null)
         {
            if(MapScaleTransform != null)
            {
               var tp = MapScaleTransform.Transform(new Point(core.renderOffset.X, core.renderOffset.Y));
               MapOverlayTranslateTransform.X = tp.X;
               MapOverlayTranslateTransform.Y = tp.Y;

               // map is scaled already
               MapTranslateTransform.X = core.renderOffset.X;
               MapTranslateTransform.Y = core.renderOffset.Y;
            }
            else
            {
               MapTranslateTransform.X = core.renderOffset.X;
               MapTranslateTransform.Y = core.renderOffset.Y;

               MapOverlayTranslateTransform.X = MapTranslateTransform.X;
               MapOverlayTranslateTransform.Y = MapTranslateTransform.Y;
            }
         }
      }

      public Brush EmptyMapBackground = Brushes.WhiteSmoke;

      /// <summary>
      /// render map in WPF
      /// </summary>
      /// <param name="g"></param>
      private void DrawMap(DrawingContext g)
      {
         if(Equals(MapProvider, EmptyProvider.Instance) || MapProvider == null)
         {
            return;
         }

            core.tileDrawingListLock.AcquireReaderLock();
            core.Matrix.EnterReadLock();
         try
         {
            foreach(var tilePoint in core.tileDrawingList)
            {
                    core.tileRect.Location = tilePoint.PosPixel;
                    core.tileRect.OffsetNegative(core.compensationOffset);

               //if(region.IntersectsWith(Core.tileRect) || IsRotated)
               {
                  var found = false;

                  var t = core.Matrix.GetTileWithNoLock(core.Zoom, tilePoint.PosXY);
                  if(t.NotEmpty)
                  {
                     foreach(var pureImage in t.Overlays)
                     {
                         var img = (GMapImage)pureImage;
                         if (img?.Img == null) continue;
                         if(!found)
                             found = true;

                         var imgRect = new Rect(core.tileRect.X + 0.6, core.tileRect.Y + 0.6, core.tileRect.Width + 0.6, core.tileRect.Height + 0.6);
                         if(!img.IsParent)
                         {
                             g.DrawImage(img.Img, imgRect);
                         }
                         else
                         {
                             // TODO: move calculations to loader thread
                             var geometry = new RectangleGeometry(imgRect);
                             var parentImgRect = new Rect(core.tileRect.X - core.tileRect.Width * img.Xoff + 0.6, core.tileRect.Y - core.tileRect.Height * img.Yoff + 0.6, core.tileRect.Width * img.Ix + 0.6, core.tileRect.Height * img.Ix + 0.6);

                             g.PushClip(geometry);
                             g.DrawImage(img.Img, parentImgRect);
                             g.Pop();
                         }
                     }
                  }
                  else if(FillEmptyTiles && MapProvider.Projection is MercatorProjection)
                  {
                     #region -- fill empty tiles --
                     int zoomOffset = 1;
                     Tile parentTile = Tile.Empty;
                     long ix = 0;

                     while(!parentTile.NotEmpty && zoomOffset < core.Zoom && zoomOffset <= LevelsKeepInMemmory)
                     {
                        ix = (long)Math.Pow(2, zoomOffset);
                        parentTile = core.Matrix.GetTileWithNoLock(core.Zoom - zoomOffset++, new GPoint((int)(tilePoint.PosXY.X / ix), (int)(tilePoint.PosXY.Y / ix)));
                     }

                     if(parentTile.NotEmpty)
                     {
                        var xoff = Math.Abs(tilePoint.PosXY.X - (parentTile.Pos.X * ix));
                        var yoff = Math.Abs(tilePoint.PosXY.Y - (parentTile.Pos.Y * ix));

                        var geometry = new RectangleGeometry(new Rect(core.tileRect.X + 0.6, core.tileRect.Y + 0.6, core.tileRect.Width + 0.6, core.tileRect.Height + 0.6));
                        var parentImgRect = new Rect(core.tileRect.X - core.tileRect.Width * xoff + 0.6, core.tileRect.Y - core.tileRect.Height * yoff + 0.6, core.tileRect.Width * ix + 0.6, core.tileRect.Height * ix + 0.6);

                        // render tile 
                        {
                           foreach(var pureImage in parentTile.Overlays)
                           {
                               var img = (GMapImage)pureImage;
                               if (img?.Img == null || img.IsParent) continue;
                               if(!found)
                                   found = true;

                               g.PushClip(geometry);
                               g.DrawImage(img.Img, parentImgRect);
                               g.DrawRectangle(SelectedAreaFill, null, geometry.Bounds);
                               g.Pop();
                           }
                        }
                     }
                     #endregion
                  }

                  // add text if tile is missing
                  if(!found)
                  {
                     lock(core.FailedLoads)
                     {
                        var lt = new LoadTask(tilePoint.PosXY, core.Zoom);

                        if(core.FailedLoads.ContainsKey(lt))
                        {
                           g.DrawRectangle(EmptytileBrush, EmptyTileBorders, new Rect(core.tileRect.X, core.tileRect.Y, core.tileRect.Width, core.tileRect.Height));

                           var ex = core.FailedLoads[lt];
                            var tileText = new FormattedText(
                                "Exception: " + ex.Message,
                                CultureInfo.CurrentUICulture,
                                FlowDirection.LeftToRight,
                                tileTypeface,
                                14,
                                Brushes.Red) { MaxTextWidth = core.tileRect.Width - 11 };

                            g.DrawText(tileText, new Point(core.tileRect.X + 11, core.tileRect.Y + 11));

                           g.DrawText(EmptyTileText, new Point(core.tileRect.X + core.tileRect.Width / 2 - EmptyTileText.Width / 2, core.tileRect.Y + core.tileRect.Height / 2 - EmptyTileText.Height / 2));
                        }
                     }
                  }

                  if(ShowTileGridLines)
                  {
                     g.DrawRectangle(null, EmptyTileBorders, new Rect(core.tileRect.X, core.tileRect.Y,
                         core.tileRect.Width, core.tileRect.Height));

                     if(tilePoint.PosXY == core.centerTileXYLocation)
                     {
                         var tileText = new FormattedText(
                             "CENTER:" + tilePoint,
                             CultureInfo.CurrentUICulture,
                             FlowDirection.LeftToRight,
                             tileTypeface,
                             16,
                             Brushes.Red) { MaxTextWidth = core.tileRect.Width };
                         g.DrawText(tileText, new Point(core.tileRect.X +
                             core.tileRect.Width / 2 - EmptyTileText.Width / 2,
                             core.tileRect.Y + core.tileRect.Height / 2 - tileText.Height / 2));
                     }
                     else
                     {
                         var tileText = new FormattedText(
                             "TILE: " + tilePoint,
                             CultureInfo.CurrentUICulture,
                             FlowDirection.LeftToRight,
                             tileTypeface,
                             16,
                             Brushes.Red) { MaxTextWidth = core.tileRect.Width };
                         g.DrawText(tileText, new Point(core.tileRect.X + core.tileRect.Width / 2 - EmptyTileText.Width / 2,
                             core.tileRect.Y + core.tileRect.Height / 2 - tileText.Height / 2));
                     }
                  }
               }
            }
         }
         finally
         {
                core.Matrix.LeaveReadLock();
                core.tileDrawingListLock.ReleaseReaderLock();
         }
      }

      /// <summary>
      /// gets image of the current view
      /// </summary>
      /// <returns></returns>
      public ImageSource ToImageSource()
      {
         FrameworkElement obj = this;

         // Save current canvas transform
         Transform transform = obj.LayoutTransform;
         obj.LayoutTransform = null;

         // fix margin offset as well
         Thickness margin = obj.Margin;
         obj.Margin = new Thickness(0, 0,
         margin.Right - margin.Left, margin.Bottom - margin.Top);

         // Get the size of canvas
         Size size = new Size(obj.ActualWidth, obj.ActualHeight);

         // force control to Update
         obj.Measure(size);
         obj.Arrange(new Rect(size));

         RenderTargetBitmap bmp = new RenderTargetBitmap(
         (int)size.Width, (int)size.Height, 96, 96, PixelFormats.Pbgra32);

         bmp.Render(obj);

         if(bmp.CanFreeze)
         {
            bmp.Freeze();
         }

         // return values as they were before
         obj.LayoutTransform = transform;
         obj.Margin = margin;

         return bmp;
      }

        /// <summary>
        /// creates path from list of points, for performance set addBlurEffect to false
        /// </summary>
        /// <param name="localPath"></param>
        /// <returns></returns>
        public virtual Path CreateRoutePath(List<Point> localPath)
      {
         return CreateRoutePath(localPath, true);
      }

       /// <summary>
       /// creates path from list of points, for performance set addBlurEffect to false
       /// </summary>       
       /// <param name="localPath"></param>
       /// <param name="addBlurEffect"></param>
       /// <returns></returns>
       public virtual Path CreateRoutePath(List<Point> localPath, bool addBlurEffect)
      {
         // Create a StreamGeometry to use to specify myPath.
         var geometry = new StreamGeometry();

         using(var ctx = geometry.Open())
         {
            ctx.BeginFigure(localPath[0], false, false);
            // Draw a line to the next specified point.
            ctx.PolyLineTo(localPath, true, true);
         }

         // Freeze the geometry (make it unmodifiable)
         // for additional performance benefits.
         geometry.Freeze();
         // Create a path to draw a geometry with.
         var myPath = new Path();
         {
            // Specify the shape of the Path using the StreamGeometry.
            myPath.Data = geometry;

            if (addBlurEffect)
            {
                var ef = new BlurEffect();
                {
                    ef.KernelType = KernelType.Gaussian;
                    ef.Radius = 3.0;
                    ef.RenderingBias = RenderingBias.Performance;
                }

                myPath.Effect = ef;
            }
            //TODO Дполнить возможностью редактора параметров линии
            myPath.Stroke = Brushes.OrangeRed;
            myPath.StrokeThickness = 5;
            myPath.StrokeLineJoin = PenLineJoin.Round;
            myPath.StrokeStartLineCap = PenLineCap.Triangle;
            myPath.StrokeEndLineCap = PenLineCap.Square;
            myPath.Opacity = 0.6;
            myPath.IsHitTestVisible = false;
         }
         return myPath;
      }

       /// <summary>
       /// creates path from list of points, for performance set addBlurEffect to false
       /// </summary>
       /// <param name="localPath"></param>
       /// <returns></returns>
       public virtual Path CreatePolygonPath(List<Point> localPath)
      {
         return CreatePolygonPath(localPath, false);
      }

       /// <summary>
       /// creates path from list of points, for performance set addBlurEffect to false
       /// </summary>
       /// <param name="localPath"></param>
       /// <param name="addBlurEffect"></param>
       /// <returns></returns>
       public virtual Path CreatePolygonPath(List<Point> localPath, bool addBlurEffect)
      {
         // Create a StreamGeometry to use to specify myPath.
         var geometry = new StreamGeometry();

         using(var ctx = geometry.Open())
         {
            ctx.BeginFigure(localPath[0], true, true);

            // Draw a line to the next specified point.
            ctx.PolyLineTo(localPath, true, true);
         }

         // Freeze the geometry (make it unmodifiable)
         // for additional performance benefits.
         geometry.Freeze();

         // Create a path to draw a geometry with.
         var path = new Path();
         {
            // Specify the shape of the Path using the StreamGeometry.
            path.Data = geometry;

            if (addBlurEffect)
            {
                var ef = new BlurEffect();
                {
                    ef.KernelType = KernelType.Gaussian;
                    ef.Radius = 3.0;
                    ef.RenderingBias = RenderingBias.Performance;
                }

                path.Effect = ef;
            }
            //TODO Дполнить возможностью редактирования параметров при создании
            path.Stroke = Brushes.CornflowerBlue;
            path.StrokeThickness = 5;
            path.StrokeLineJoin = PenLineJoin.Round;
            path.StrokeStartLineCap = PenLineCap.Triangle;
            path.StrokeEndLineCap = PenLineCap.Square;

            path.Fill = Brushes.AliceBlue;

            path.Opacity = 0.6;
            path.IsHitTestVisible = false;
         }
         return path;
      }

      /// <summary>
      /// sets zoom to max to fit rect
      /// </summary>
      /// <param name="rect">area</param>
      /// <returns></returns>
      public bool SetZoomToFitRect(RectLatLng rect)
      {
         if(lazyEvents)
         {
            lazySetZoomToFitRect = rect;
         }
         else
         {
            var maxZoom = core.GetMaxZoomToFitRect(rect);
             if (maxZoom <= 0) return false;
             var center = new PointLatLng(rect.Lat - rect.HeightLat / 2, rect.Lng + rect.WidthLng / 2);
                Position = center;

             if(maxZoom > MaxZoom)
             {
                 maxZoom = MaxZoom;
             }

             if(core.Zoom != maxZoom)
             {
                    Zoom = maxZoom;
             }

             return true;
         }
         return false;
      }

       private RectLatLng? lazySetZoomToFitRect;

       private bool lazyEvents = true;

      /// <summary>
      /// sets to max zoom to fit all markers and centers them in map
      /// </summary>
      /// <param name="zIndex">z index or null to check all</param>
      /// <returns></returns>
      public bool ZoomAndCenterMarkers(int? zIndex)
      {
         var rect = GetRectOfAllMarkers(zIndex);
         return rect.HasValue && SetZoomToFitRect(rect.Value);
      }

      /// <summary>
      /// gets rectangle with all objects inside
      /// </summary>
      /// <param name="zIndex">z index or null to check all</param>
      /// <returns></returns>
      public RectLatLng? GetRectOfAllMarkers(int? zIndex)
      {
         RectLatLng? ret = null;

         var left = double.MaxValue;
         var top = double.MinValue;
         var right = double.MinValue;
         var bottom = double.MaxValue;

          var overlays = zIndex.HasValue ? ItemsSource.Cast<GMapMarker>()
                .Where(p => p != null && p.ZIndex == zIndex) : ItemsSource.Cast<GMapMarker>();

          foreach(var m in overlays)
          {
              if (m.Shape == null || m.Shape.Visibility != Visibility.Visible) continue;
              // left
              if(m.Position.Lng < left)
              {
                  left = m.Position.Lng;
              }

              // top
              if(m.Position.Lat > top)
              {
                  top = m.Position.Lat;
              }

              // right
              if(m.Position.Lng > right)
              {
                  right = m.Position.Lng;
              }

              // bottom
              if(m.Position.Lat < bottom)
              {
                  bottom = m.Position.Lat;
              }
          }

          if(left != double.MaxValue && right != double.MinValue 
                && top != double.MinValue && bottom != double.MaxValue){
            ret = RectLatLng.FromLTRB(left, top, right, bottom);
         }

         return ret;
      }

      /// <summary>
      /// offset position in pixels
      /// </summary>
      /// <param name="x"></param>
      /// <param name="y"></param>
      public void Offset(int x, int y)
      {
          if (!IsLoaded) return;
          if(IsRotated)
          {
              var p = new Point(x, y);
              p = rotationMatrixInvert.Transform(p);
              x = (int)p.X;
              y = (int)p.Y;

                core.DragOffset(new GPoint(x, y));

                ForceUpdateOverlays();
          }
          else
          {
                core.DragOffset(new GPoint(x, y));

                UpdateMarkersOffset();
                InvalidateVisual(true);
          }
      }

       private readonly RotateTransform rotationMatrix = new RotateTransform();
      GeneralTransform rotationMatrixInvert = new RotateTransform();

      /// <summary>
      /// updates rotation matrix
      /// </summary>
      private void UpdateRotationMatrix()
      {
         var center = new Point(ActualWidth / 2.0, ActualHeight / 2.0);

         rotationMatrix.Angle = -Bearing;
         rotationMatrix.CenterY = center.Y;
         rotationMatrix.CenterX = center.X;

         rotationMatrixInvert = rotationMatrix.Inverse;
      }

      /// <summary>
      /// returs true if map bearing is not zero
      /// </summary>         
      public bool IsRotated => core.IsRotated;

        /// <summary>
      /// bearing for rotation of the map
      /// </summary>
      [Category("GMap.NET")]
      public float Bearing
      {
         get
         {
            return core.bearing;
         }
         set
         {
             if (core.bearing == value) return;
             var resize = core.bearing == 0;
                core.bearing = value;

                UpdateRotationMatrix();

             if(value != 0 && value % 360 != 0)
             {
                    core.IsRotated = true;

                 if(core.tileRectBearing.Size == core.tileRect.Size)
                 {
                        core.tileRectBearing = core.tileRect;
                        core.tileRectBearing.Inflate(1, 1);
                 }
             }
             else
             {
                    core.IsRotated = false;
                    core.tileRectBearing = core.tileRect;
             }

             if(resize)
             {
                    core.OnMapSizeChanged((int)ActualWidth, (int)ActualHeight);
             }

             if(core.IsStarted)
             {
                    ForceUpdateOverlays();
             }
         }
      }

      /// <summary>
      /// apply transformation if in rotation mode
      /// </summary>
      private Point ApplyRotation(double x, double y)
      {
         var ret = new Point(x, y);

         if(IsRotated)
         {
            ret = rotationMatrix.Transform(ret);
         }

         return ret;
      }

      /// <summary>
      /// apply transformation if in rotation mode
      /// </summary>
      private Point ApplyRotationInversion(double x, double y)
      {
         var ret = new Point(x, y);

         if(IsRotated)
         {
            ret = rotationMatrixInvert.Transform(ret);
         }

         return ret;
      }

      #region UserControl Events
      protected override void OnRender(DrawingContext drawingContext)
      {
         if(!core.IsStarted)
            return;

         drawingContext.DrawRectangle(EmptyMapBackground, null, new Rect(RenderSize));

         if(IsRotated)
         {
            drawingContext.PushTransform(rotationMatrix);

            if(MapScaleTransform != null)
            {
               drawingContext.PushTransform(MapScaleTransform); 
               drawingContext.PushTransform(MapTranslateTransform);
               {
                  DrawMap(drawingContext);
               }
               drawingContext.Pop();
               drawingContext.Pop();
            }
            else
            {
               drawingContext.PushTransform(MapTranslateTransform);
               {
                  DrawMap(drawingContext);
               }
               drawingContext.Pop();
            }

            drawingContext.Pop();
         }
         else
         {
            if(MapScaleTransform != null)
            {
               drawingContext.PushTransform(MapScaleTransform);
               drawingContext.PushTransform(MapTranslateTransform);
               {
                  DrawMap(drawingContext);

#if DEBUG
                  drawingContext.DrawLine(virtualCenterCrossPen, new Point(-20, 0), new Point(20, 0));
                  drawingContext.DrawLine(virtualCenterCrossPen, new Point(0, -20), new Point(0, 20));
#endif
               }
               drawingContext.Pop();
               drawingContext.Pop();
            }
            else
            {
               drawingContext.PushTransform(MapTranslateTransform);
               {
                  DrawMap(drawingContext);
#if DEBUG
                  drawingContext.DrawLine(virtualCenterCrossPen, new Point(-20, 0), new Point(20, 0));
                  drawingContext.DrawLine(virtualCenterCrossPen, new Point(0, -20), new Point(0, 20));
#endif
               }
               drawingContext.Pop();
            }
         }

         // selection
         if(!SelectedArea.IsEmpty)
         {
            var p1 = FromLatLngToLocal(SelectedArea.LocationTopLeft);
            var p2 = FromLatLngToLocal(SelectedArea.LocationRightBottom);

            var x1 = p1.X;
            var y1 = p1.Y;
            var x2 = p2.X;
            var y2 = p2.Y;

            if(SelectionUseCircle)
            {
               drawingContext.DrawEllipse(SelectedAreaFill, SelectionPen, 
                   new Point(x1 + (x2 - x1) / 2, y1 + (y2 - y1) / 2), (x2 - x1) / 2, (y2 - y1) / 2);
            }
            else
            {
               drawingContext.DrawRoundedRectangle(SelectedAreaFill, SelectionPen, new Rect(x1, y1, x2 - x1, y2 - y1), 5, 5);
            }
         }

         if(ShowCenter)
         {
            drawingContext.DrawLine(CenterCrossPen, 
                new Point(ActualWidth / 2 - 5, ActualHeight / 2), 
                new Point(ActualWidth / 2 + 5, ActualHeight / 2));
            drawingContext.DrawLine(CenterCrossPen, 
                new Point(ActualWidth / 2, ActualHeight / 2 - 5), 
                new Point(ActualWidth / 2, ActualHeight / 2 + 5));
         }

         if(renderHelperLine)
         {
            var p = Mouse.GetPosition(this);

            drawingContext.DrawLine(HelperLinePen, 
                new Point(p.X, 0), 
                new Point(p.X, ActualHeight));
            drawingContext.DrawLine(HelperLinePen, 
                new Point(0, p.Y),
                new Point(ActualWidth, p.Y));
         }

         #region -- copyright --

         if(copyright != null)
         {
            drawingContext.DrawText(copyright, 
                new Point(5, ActualHeight - copyright.Height - 5));
         }

         #endregion

         base.OnRender(drawingContext);
      }

      public Pen CenterCrossPen = new Pen(Brushes.Transparent, 0);
      public bool ShowCenter = true;

#if DEBUG
       private readonly Pen virtualCenterCrossPen = new Pen(Brushes.Transparent, 0);
#endif

       private HelperLineOptions helperLineOption = HelperLineOptions.DontShow;

      /// <summary>
      /// draw lines at the mouse pointer position
      /// </summary>
      [Browsable(false)]
      public HelperLineOptions HelperLineOption
      {
         get
         {
            return helperLineOption;
         }
         set
         {
            helperLineOption = value;
            renderHelperLine = helperLineOption == HelperLineOptions.ShowAlways;
            if(core.IsStarted)
            {
               InvalidateVisual();
            }
         }
      }

      public Pen HelperLinePen = new Pen(Brushes.Blue, 1);

       private bool renderHelperLine;

      protected override void OnKeyUp(KeyEventArgs e)
      {
         base.OnKeyUp(e);

          if (HelperLineOption != HelperLineOptions.ShowOnModifierKey) return;
            renderHelperLine = !(e.IsUp && (e.Key == Key.LeftShift || e.SystemKey == Key.LeftAlt));
          if(!renderHelperLine)
          {
                InvalidateVisual();
          }
      }

      protected override void OnKeyDown(KeyEventArgs e)
      {
         base.OnKeyDown(e);
         
         if(HelperLineOption == HelperLineOptions.ShowOnModifierKey)
         {
            renderHelperLine = e.IsDown && (e.Key == Key.LeftShift || e.SystemKey == Key.LeftAlt);
            if(renderHelperLine)
            {
               InvalidateVisual();
            }
         }         
      }

       /// <summary>
       /// reverses MouseWheel zooming direction
       /// </summary>
       public  bool InvertedMouseWheelZooming = false;

       /// <summary>
      /// lets you zoom by MouseWheel even when pointer is in area of marker
      /// </summary>
      public bool IgnoreMarkerOnMouseWheel = false;

      protected override void OnMouseWheel(MouseWheelEventArgs e)
      {
         base.OnMouseWheel(e);

          if (!MouseWheelZoomEnabled || (!IsMouseDirectlyOver && !IgnoreMarkerOnMouseWheel)
              || core.IsDragging) return;
          var p = e.GetPosition(this);

          var generalTransform = MapScaleTransform?.Inverse;
          if (generalTransform != null) p = generalTransform.Transform(p);

          p = ApplyRotationInversion(p.X, p.Y);

          if(core.mouseLastZoom.X != (int)p.X && core.mouseLastZoom.Y != (int)p.Y)
          {
              if(MouseWheelZoomType == MouseWheelZoomType.MousePositionAndCenter)
              {
                    core.position = FromLocalToLatLng((int)p.X, (int)p.Y);
              }
              else if(MouseWheelZoomType == MouseWheelZoomType.ViewCenter)
              {
                    core.position = FromLocalToLatLng((int)ActualWidth / 2, (int)ActualHeight / 2);
              }
              else if(MouseWheelZoomType == MouseWheelZoomType.MousePositionWithoutCenter)
              {
                    core.position = FromLocalToLatLng((int)p.X, (int)p.Y);
              }

                core.mouseLastZoom.X = (int)p.X;
                core.mouseLastZoom.Y = (int)p.Y;
          }

          // set mouse position to map center
          if(MouseWheelZoomType != MouseWheelZoomType.MousePositionWithoutCenter)
          {
              var ps = PointToScreen(new Point(ActualWidth / 2, ActualHeight / 2));
              Stuff.SetCursorPos((int)ps.X, (int)ps.Y);
          }

            core.MouseWheelZooming = true;

          if(e.Delta > 0)
          {
              if(!InvertedMouseWheelZooming)
              {
                    Zoom = (int)Zoom + 1;
              }
              else
              {
                    Zoom = (int)(Zoom + 0.99) - 1;
              }
          }
          else
          {
              if(InvertedMouseWheelZooming)
              {
                    Zoom = (int)Zoom + 1;
              }
              else
              {
                    Zoom = (int)(Zoom + 0.99) - 1;
              }
          }

            core.MouseWheelZooming = false;
      }

       private bool isSelected;

      protected override void OnMouseDown(MouseButtonEventArgs e)
      {
         base.OnMouseDown(e);
          
         if(CanDragMap && e.ChangedButton == DragButton)
         {
            var p = e.GetPosition(this);

             var generalTransform = MapScaleTransform?.Inverse;
             if (generalTransform != null) p = generalTransform.Transform(p);

             p = ApplyRotationInversion(p.X, p.Y);

                core.mouseDown.X = (int)p.X;
                core.mouseDown.Y = (int)p.Y;

            InvalidateVisual();
         }
         else
         {
             if (isSelected) return;
             var p = e.GetPosition(this);
                isSelected = true;
                SelectedArea = RectLatLng.Empty;
                selectionEnd = PointLatLng.Empty;
                selectionStart = FromLocalToLatLng((int)p.X, (int)p.Y);
         }         
      }

       private int onMouseUpTimestamp;

      protected override void OnMouseUp(MouseButtonEventArgs e)
      {
         base.OnMouseUp(e);
          
         if(isSelected)
         {
            isSelected = false;
         }

         if(core.IsDragging)
         {
            if(IsDragging)
            {
               onMouseUpTimestamp = e.Timestamp & MaxValue;
                    IsDragging = false;
               Debug.WriteLine("IsDragging = " + IsDragging);
               Cursor = cursorBefore;
               Mouse.Capture(null);
            }
                core.EndDrag();

             if (!BoundsOfMap.HasValue || BoundsOfMap.Value.Contains(Position)) return;
             if(core.LastLocationInBounds.HasValue)
             {
                    Position = core.LastLocationInBounds.Value;
             }
         }
         else
         {
            if(e.ChangedButton == DragButton)
            {
                    core.mouseDown = GPoint.Empty;
            }

            if(!selectionEnd.IsEmpty && !selectionStart.IsEmpty)
            {
               var zoomtofit = false;

               if(!SelectedArea.IsEmpty && Keyboard.Modifiers == ModifierKeys.Shift)
               {
                  zoomtofit = SetZoomToFitRect(SelectedArea);
               }

                    OnSelectionChange?.Invoke(SelectedArea, zoomtofit);
            }
            else
            {
               InvalidateVisual();
            }
         }
      }

        private Cursor cursorBefore = Cursors.Arrow;

      protected override void OnMouseMove(MouseEventArgs e)
      {
         base.OnMouseMove(e);
          
         // wpf generates to many events if mouse is over some visual
         // and OnMouseUp is fired, wtf, anyway...
         // http://greatmaps.codeplex.com/workitem/16013
         if((e.Timestamp & MaxValue) - onMouseUpTimestamp < 55)
         {
            Debug.WriteLine("OnMouseMove skipped: " + ((e.Timestamp & MaxValue) - onMouseUpTimestamp) + "ms");
            return;
         }

         if(!core.IsDragging && !core.mouseDown.IsEmpty)
         {
            var p = e.GetPosition(this);
             var generalTransform= MapScaleTransform?.Inverse;
             if (generalTransform != null) p = generalTransform.Transform(p);

             p = ApplyRotationInversion(p.X, p.Y);

            // cursor has moved beyond drag tolerance
            if(Math.Abs(p.X - core.mouseDown.X) * 2 >= SystemParameters.MinimumHorizontalDragDistance || Math.Abs(p.Y - core.mouseDown.Y) * 2 
                    >= SystemParameters.MinimumVerticalDragDistance)
            {
                    core.BeginDrag(core.mouseDown);
            }
         }

         if(core.IsDragging)
         {
            if(!IsDragging)
            {
                    IsDragging = true;
               Debug.WriteLine("IsDragging = " + IsDragging);
               cursorBefore = Cursor;
               Cursor = Cursors.SizeAll;
               Mouse.Capture(this);
            }

            if(BoundsOfMap.HasValue && !BoundsOfMap.Value.Contains(Position))
            {
               // ...
            }
            else
            {
               var p = e.GetPosition(this);

                var generalTransform = MapScaleTransform?.Inverse;
                if (generalTransform != null) p = generalTransform.Transform(p);

                p = ApplyRotationInversion(p.X, p.Y);

                    core.mouseCurrent.X = (int)p.X;
                    core.mouseCurrent.Y = (int)p.Y;
               {
                        core.Drag(core.mouseCurrent);
               }

               if(IsRotated || scaleMode != ScaleModes.Integer)
               {
                  ForceUpdateOverlays();
               }
               else
               {
                  UpdateMarkersOffset();
               }
            }
            InvalidateVisual(true);
         }
         else
         {
            if(isSelected && !selectionStart.IsEmpty && 
                    (Keyboard.Modifiers == ModifierKeys.Shift || Keyboard.Modifiers == ModifierKeys.Alt || DisableAltForSelection))
            {
               var p = e.GetPosition(this);
               selectionEnd = FromLocalToLatLng((int)p.X, (int)p.Y);
               {
                  var p1 = selectionStart;
                  var p2 = selectionEnd;

                  var x1 = Math.Min(p1.Lng, p2.Lng);
                  var y1 = Math.Max(p1.Lat, p2.Lat);
                  var x2 = Math.Max(p1.Lng, p2.Lng);
                  var y2 = Math.Min(p1.Lat, p2.Lat);

                  SelectedArea = new RectLatLng(y1, x1, x2 - x1, y1 - y2);
               }
            }

            if(renderHelperLine)
            {
               InvalidateVisual(true);
            }
         }         
      }

      /// <summary>
      /// if true, selects area just by holding mouse and moving
      /// </summary>
      public bool DisableAltForSelection = false;

      protected override void OnStylusDown(StylusDownEventArgs e)
      {
         base.OnStylusDown(e);

          if (!TouchEnabled || !CanDragMap || e.InAir) return;
          var p = e.GetPosition(this);

          var generalTransform = MapScaleTransform?.Inverse;
          if (generalTransform != null) p = generalTransform.Transform(p);

          p = ApplyRotationInversion(p.X, p.Y);

            core.mouseDown.X = (int)p.X;
            core.mouseDown.Y = (int)p.Y;

            InvalidateVisual();
      }

      protected override void OnStylusUp(StylusEventArgs e)
      {
         base.OnStylusUp(e);
          
         if(TouchEnabled)
         {
            if(isSelected)
            {
               isSelected = false;
            }

            if(core.IsDragging)
            {
               if(IsDragging)
               {
                  onMouseUpTimestamp = e.Timestamp & MaxValue;
                        IsDragging = false;
                  Debug.WriteLine("IsDragging = " + IsDragging);
                  Cursor = cursorBefore;
                  Mouse.Capture(null);
               }
                    core.EndDrag();

                if (!BoundsOfMap.HasValue || BoundsOfMap.Value.Contains(Position)) return;
                if(core.LastLocationInBounds.HasValue)
                {
                        Position = core.LastLocationInBounds.Value;
                }
            }
            else
            {
                    core.mouseDown = GPoint.Empty;
               InvalidateVisual();
            }
         }
      }

      protected override void OnStylusMove(StylusEventArgs e)
      {
         base.OnStylusMove(e);

          if (!TouchEnabled) return;
          // wpf generates to many events if mouse is over some visual
          // and OnMouseUp is fired, wtf, anyway...
          // http://greatmaps.codeplex.com/workitem/16013
          if((e.Timestamp & MaxValue) - onMouseUpTimestamp < 55)
          {
              Debug.WriteLine("OnMouseMove skipped: " + ((e.Timestamp & MaxValue) - onMouseUpTimestamp) + "ms");
              return;
          }

          if(!core.IsDragging && !core.mouseDown.IsEmpty)
          {
              var p = e.GetPosition(this);

              var generalTransform = MapScaleTransform?.Inverse;
              if (generalTransform != null) p = generalTransform.Transform(p);

              p = ApplyRotationInversion(p.X, p.Y);

              // cursor has moved beyond drag tolerance
              if(Math.Abs(p.X - core.mouseDown.X) * 2 >= SystemParameters.MinimumHorizontalDragDistance || 
                    Math.Abs(p.Y - core.mouseDown.Y) * 2 >= SystemParameters.MinimumVerticalDragDistance)
              {
                    core.BeginDrag(core.mouseDown);
              }
          }

          if (!core.IsDragging) return;
          {
              if(!IsDragging)
              {
                    IsDragging = true;
                  Debug.WriteLine("IsDragging = " + IsDragging);
                    cursorBefore = Cursor;
                    Cursor = Cursors.SizeAll;
                  Mouse.Capture(this);
              }

              if(BoundsOfMap.HasValue && !BoundsOfMap.Value.Contains(Position))
              {
                  // ...
              }
              else
              {
                  var p = e.GetPosition(this);

                  var generalTransform = MapScaleTransform?.Inverse;
                  if (generalTransform != null) p = generalTransform.Transform(p);

                  p = ApplyRotationInversion(p.X, p.Y);

                    core.mouseCurrent.X = (int)p.X;
                    core.mouseCurrent.Y = (int)p.Y;
                  {
                        core.Drag(core.mouseCurrent);
                  }

                  if(IsRotated)
                  {
                        ForceUpdateOverlays();
                  }
                  else
                  {
                        UpdateMarkersOffset();
                  }
              }
                InvalidateVisual();
          }
      }

      #endregion

      #region IGControl Members

      /// <summary>
      /// Call it to empty tile cache & reload tiles
      /// </summary>
      public void ReloadMap()
      {
            core.ReloadMap();
      }

      /// <summary>
      /// sets position using geocoder
      /// </summary>
      /// <param name="keys"></param>
      /// <returns></returns>
      public GeoCoderStatusCode SetPositionByKeywords(string keys)
      {
         var status = GeoCoderStatusCode.Unknow;
         var gp = MapProvider as GeocodingProvider ?? GMapProviders.OpenStreetMap;
          if (gp == null) return status;
          var pt = gp.GetPoint(keys, out status);
          if(status == GeoCoderStatusCode.G_GEO_SUCCESS && pt.HasValue)
          {
                Position = pt.Value;
          }

          return status;
      }

      public PointLatLng FromLocalToLatLng(int x, int y)
      {
          var generalTransform = MapScaleTransform?.Inverse;
          if (generalTransform != null)
          {
              var tp = generalTransform.Transform(new Point(x, y));
              x = (int)tp.X;
              y = (int)tp.Y;
          }
          if (!IsRotated) return core.FromLocalToLatLng(x, y);
          var f = rotationMatrixInvert.Transform(new Point(x, y));

          x = (int)f.X;
          y = (int)f.Y;

          return core.FromLocalToLatLng(x, y);
      }

      public GPoint FromLatLngToLocal(PointLatLng point)
      {
         var ret = core.FromLatLngToLocal(point);

         if(MapScaleTransform != null)
         {
            var tp = MapScaleTransform.Transform(new Point(ret.X, ret.Y));
            ret.X = (int)tp.X;
            ret.Y = (int)tp.Y;
         }

         if(IsRotated)
         {
            var f = rotationMatrix.Transform(new Point(ret.X, ret.Y));

            ret.X = (int)f.X;
            ret.Y = (int)f.Y;
         }

         return ret;
      }

      public bool ShowExportDialog()
      {
         var dlg = new Microsoft.Win32.SaveFileDialog();
         {
            dlg.CheckPathExists = true;
            dlg.CheckFileExists = false;
            dlg.AddExtension = true;
            dlg.DefaultExt = "gmdb";
            dlg.ValidateNames = true;
            dlg.Title = "GMap.NET: Export map to db, if file exsist only new data will be added";
            dlg.FileName = "DataExp";
            dlg.InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            dlg.Filter = "GMap.NET DB files (*.gmdb)|*.gmdb";
            dlg.FilterIndex = 1;
            dlg.RestoreDirectory = true;

             if (dlg.ShowDialog() != true) return false;
             var ok = GMaps.Instance.ExportToGMDB(dlg.FileName);
             if(ok)
             {
                 MessageBox.Show("Complete!", "GMap.NET", MessageBoxButton.OK, MessageBoxImage.Information);
             }
             else
             {
                 MessageBox.Show("  Failed!", "GMap.NET", MessageBoxButton.OK, MessageBoxImage.Warning);
             }

             return ok;
         }
      }

      public bool ShowImportDialog()
      {
         var dlg = new Microsoft.Win32.OpenFileDialog();
         {
            dlg.CheckPathExists = true;
            dlg.CheckFileExists = false;
            dlg.AddExtension = true;
            dlg.DefaultExt = "gmdb";
            dlg.ValidateNames = true;
            dlg.Title = "GMap.NET: Import to db, only new data will be added";
            dlg.FileName = "DataImport";
            dlg.InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            dlg.Filter = "GMap.NET DB files (*.gmdb)|*.gmdb";
            dlg.FilterIndex = 1;
            dlg.RestoreDirectory = true;

            if(dlg.ShowDialog() == true)
            {
               Cursor = Cursors.Wait;

               bool ok = GMaps.Instance.ImportFromGMDB(dlg.FileName);
               if(ok)
               {
                  MessageBox.Show("Complete!", "GMap.NET", MessageBoxButton.OK, MessageBoxImage.Information);
                  ReloadMap();
               }
               else
               {
                  MessageBox.Show("  Failed!", "GMap.NET", MessageBoxButton.OK, MessageBoxImage.Warning);
               }

               Cursor = Cursors.Arrow;

               return ok;
            }
         }

         return false;
      }

      /// <summary>
      /// current coordinates of the map center
      /// </summary>
      [Browsable(false)]
      public PointLatLng Position
      {
         get
         {
            return core.Position;
         }
         set
         {
                core.Position = value;

            if(core.IsStarted)
            {
               ForceUpdateOverlays();
            }
         }
      }

      [Browsable(false)]
      public GPoint PositionPixel => core.PositionPixel;

        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
      [Browsable(false)]
      public string CacheLocation
      {
         get
         {
            return CacheLocator.Location;
         }
         set
         {
            CacheLocator.Location = value;
         }
      }

      [Browsable(false)]
      public bool IsDragging { get; private set; }

      [Browsable(false)]
      public RectLatLng ViewArea
      {
         get
         {
             if(!IsRotated)
             {
                 return core.ViewArea;
             }
             if (core.Provider.Projection == null) return RectLatLng.Empty;
             var p = FromLocalToLatLng(0, 0);
             var p2 = FromLocalToLatLng((int)Width, (int)Height);
    
             return RectLatLng.FromLTRB(p.Lng, p.Lat, p2.Lng, p2.Lat);
         }
      }

      [Category("GMap.NET")]
      public bool CanDragMap
      {
         get
         {
            return core.CanDragMap;
         }
         set
         {
                core.CanDragMap = value;
         }
      }

      public RenderMode RenderMode => RenderMode.WPF;

        #endregion

      #region IGControl event Members

      public event PositionChanged OnPositionChanged
      {
         add
         {
                core.OnCurrentPositionChanged += value;
         }
         remove
         {
                core.OnCurrentPositionChanged -= value;
         }
      }

      public event TileLoadComplete OnTileLoadComplete
      {
         add
         {
                core.OnTileLoadComplete += value;
         }
         remove
         {
                core.OnTileLoadComplete -= value;
         }
      }

      public event TileLoadStart OnTileLoadStart
      {
         add
         {
                core.OnTileLoadStart += value;
         }
         remove
         {
                core.OnTileLoadStart -= value;
         }
      }

      public event MapDrag OnMapDrag
      {
         add
         {
                core.OnMapDrag += value;
         }
         remove
         {
                core.OnMapDrag -= value;
         }
      }

      public event MapZoomChanged OnMapZoomChanged
      {
         add
         {
                core.OnMapZoomChanged += value;
         }
         remove
         {
                core.OnMapZoomChanged -= value;
         }
      }

      /// <summary>
      /// occures on map type changed
      /// </summary>
      public event MapTypeChanged OnMapTypeChanged
      {
         add
         {
                core.OnMapTypeChanged += value;
         }
         remove
         {
                core.OnMapTypeChanged -= value;
         }
      }

      /// <summary>
      /// occurs on empty tile displayed
      /// </summary>
      public event EmptyTileError OnEmptyTileError
      {
         add
         {
                core.OnEmptyTileError += value;
         }
         remove
         {
                core.OnEmptyTileError -= value;
         }
      }
      #endregion

      #region IDisposable Members

      public virtual void Dispose()
      {
          if (!core.IsStarted) return;
            core.OnMapZoomChanged -= ForceUpdateOverlays;
            Loaded -= GMapControlLoaded;
            Dispatcher.ShutdownStarted -= DispatcherShutdownStarted;
            SizeChanged -= GMapControlSizeChanged;
          if(loadedApp != null)
          {
                loadedApp.SessionEnding -= CurrentSessionEnding;
          }
            core.OnMapClose();
      }

      #endregion
   }

   public enum HelperLineOptions
   {
      DontShow = 0,
      ShowAlways = 1,
      ShowOnModifierKey = 2
   }

   public enum ScaleModes
   {
      /// <summary>
      /// no scaling
      /// </summary>
      Integer,

      /// <summary>
      /// scales to fractional level using a stretched tiles, any issues -> http://greatmaps.codeplex.com/workitem/16046
      /// </summary>
      ScaleUp,

      /// <summary>
      /// scales to fractional level using a narrowed tiles, any issues -> http://greatmaps.codeplex.com/workitem/16046
      /// </summary>
      ScaleDown,

      /// <summary>
      /// scales to fractional level using a combination both stretched and narrowed tiles, any issues -> http://greatmaps.codeplex.com/workitem/16046
      /// </summary>
      Dynamic
   }

   public delegate void SelectionChange(RectLatLng selection, bool zoomToFit);
}
