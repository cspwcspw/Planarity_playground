using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;
using Xceed.Wpf.Toolkit;

// Pete, March/April 2022. 
// Played Aug 23, this comment creates a trivial change that will allow me to 
// verify that my ssh keys work ok on github. 

namespace Planarity
{

    public partial class MainWindow : Window
    {

        public const double VSz = 36;
        public const double HalfVSz = VSz / 2.0;

        Brush VNormalFill = Brushes.LightBlue;
        Brush VHighlightFill = Brushes.Red;
        Brush VStroke = Brushes.Black;

        Brush EStroke = Brushes.Blue;
        Brush EStrokeHighlight = Brushes.Red;

        Brush UnsolvedBrush = Brushes.LightGray;
        Brush SolvedBrush = Brushes.LightGreen;

        Shape vertexBeingDragged = null;

        Graph theGraph;


        // Hosts the playground and debug textbox, provides background colour, etc
        DockPanel outerPanel;  
        Canvas playground;  // The graph Nodes and Edges live in the playground.
        TextBox debug;      // The debug panel is usuallu collapsed / invisible.
 
        
        Matrix playgroundTransform;  // Controls zoom and dragging / panning of rendered playground relative to the outerPanel
        Point mouseLastSeenAt;       // Used when dragging the playground around. (i.e. it looks like you are dragging the graph)


        IntegerUpDown numVerts;     // User control to set the game size

        DateTime gameStartTime;

        // Some labels to show feedback to the user.
        Label numCrossings;
        Label elapsedTime;

        DispatcherTimer tickTocker;

        public MainWindow()
        {
            InitializeComponent();

            //  I prefer to build my GUI in code.

            Menu main = new Menu() { };
            mainGrid.Children.Add(main);

            MenuItem tools = new MenuItem() { Header = "Tools" };
            main.Items.Add(tools);

            MenuItem help= new MenuItem() { Header = "Help" };
            help.Click += Help_Click;
            main.Items.Add(help);


            // tools.Items.Add(newGame);

            MenuItem showHide = new MenuItem() { Header = "Show / Hide debug panel" };
            showHide.Click += (object sender, RoutedEventArgs e) =>
               { debug.Visibility = (debug.Visibility == Visibility.Visible) ? Visibility.Collapsed : Visibility.Visible; };
            tools.Items.Add(showHide);

            
            StackPanel sp = new StackPanel() 
            {  Orientation= Orientation.Horizontal     };
            sp.Children.Add(new Label() { Content = "Vertices" });
            numVerts = new IntegerUpDown() { Width = 40,  Value = 8, Minimum = 4, Maximum = 200, AllowTextInput=true};
            sp.Children.Add(numVerts);
            Button newGame = new Button() { Content = "New game", Margin = new Thickness(4, 2, 4, 2) };
            newGame.Click += (object sender, RoutedEventArgs e) => { NewGame(); };
            sp.Children.Add(newGame);
            main.Items.Add(sp);


            // Add a label, used to dynamically update the number of crossing lines during gameplay
            numCrossings = new Label() { };
            main.Items.Add(numCrossings);

            // Add a label, used to dynamically update the number of crossing lines during gameplay
            elapsedTime = new Label() { };
            main.Items.Add(elapsedTime);

            outerPanel = new DockPanel() { Background=UnsolvedBrush, Margin = new Thickness(0, 28, 0, 0) };
            mainGrid.Children.Add(outerPanel);
            mainGrid.MouseDown += MainGrid_MouseDown;
            mainGrid.MouseMove += MainGrid_MouseMove;
            playground = new Canvas() { Background = Brushes.LightBlue  };
            outerPanel.Children.Add(playground);

            // Put playground origin at middle of window to begin with
            double scale = 0.8;
            playgroundTransform = new Matrix(scale, 0, 0, scale, Width / 2, Height / 2);
            playground.RenderTransform = new MatrixTransform(playgroundTransform);

            // Set up my debug textbox, initially collapsed
            debug = new TextBox() { Width = 280, 
                HorizontalScrollBarVisibility = ScrollBarVisibility.Auto, 
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Stretch,
                Visibility = Visibility.Collapsed
            };
            // Clear the textbox on a double-click
            debug.MouseDoubleClick += (object sender, MouseButtonEventArgs e) => { debug.Clear(); };

            DockPanel.SetDock(debug, Dock.Right);
            outerPanel.Children.Add(debug);

            tickTocker = new DispatcherTimer() { Interval = TimeSpan.FromMilliseconds(1000) };
            tickTocker.Tick += (object? sender, EventArgs e) =>
             { elapsedTime.Content = $"Elapsed time {(int)(DateTime.Now-gameStartTime).TotalSeconds} secs"; };

            NewGame();
        }

 

        private void Help_Click(object sender, RoutedEventArgs e)
        {
            string helpText =
                "Drag the nodes (disks) about so that no lines cross each other.\n" +
                "\nUse the Mouse Wheel to zoom in or out." +
                "\nDragging the mouse while holding down the Right Mouse Button moves everything." +
                "\n\nMy game generator occasionally leaves nodes out on a limb." +
                "\nIt is a bit quirky, so I call it a 'feature', not a bug." +
                "\n\nRead about Planarity at Wikipedia." +
                "\nJason Davies has a great version that runs in a browser.";

            System.Windows.MessageBox.Show(helpText, "Planarity help:");
        }

        #region Write to my debug textbox

        public void say(string s)
        {
            // This is slow, so don't do anything if the debug window is not visible.
            if (debug.Visibility == Visibility.Visible)
            {
                debug.Text += s;
                debug.ScrollToEnd();
            }
        }

        #endregion

        void NewGame()
        {
            debug.Clear();
            theGraph = new Graph(this, (int) numVerts.Value);  
            PlaceGraphOnPlayground(theGraph);
            gameStartTime = DateTime.Now;
            tickTocker.IsEnabled = true;
        }

        private void showOrigin()  // Sometimes nice to use this for panning / zoom operations
        {
            Line yAxis = new Line() { X1 = 0, Y1 = -100, X2 = 0, Y2 = 100, Stroke = Brushes.Black };
            playground.Children.Add(yAxis);
            Line xAxis = new Line() { X1 = -100, Y1 = 0, X2 = 100, Y2 = 0, Stroke = Brushes.Black };
            playground.Children.Add(xAxis);
        }

        #region Create UI Widgets and place them when game starts. They are not installed on the playground canvas intially.
        internal Shape MakeNodeWidget(Vertex vertex)
        {
            Shape widget = new Ellipse() { Fill = VNormalFill, Stroke = VStroke, Width = VSz, Height = VSz, ToolTip=$"{vertex.vNum}", Tag= vertex};
            widget.MouseEnter += V_MouseEnter;   // Attach handlers to every node we create.
            widget.MouseLeave += V_MouseLeave;
            widget.MouseDown += V_MouseDown;
            widget.MouseUp += V_MouseUp;
            widget.MouseMove += V_MouseMove;
            return widget;
        }

        internal Line MakeEdgeWidget(Edge e)
        {
            say($"Adding edge {e.eNum} ({e.VFrom.vNum} -> {e.VTo.vNum})\n");
            Line widget = new Line()  { Stroke = EStroke };
            return widget;
        }

        private void PlaceGraphOnPlayground(Graph g)
        {
            playground.Children.Clear();
            // Place the edges first so they appear to be underneath the vertices.
            foreach (Edge e in g.Edges)
            {
                playground.Children.Add(e.UIEdge);
            }

            foreach (Vertex vert in g.Vertices)
            {
                playground.Children.Add(vert.UIWidget);  
            }
            scrambleNodesIntoRandomStartingConfiguration();
        }

        private void scrambleNodesIntoRandomStartingConfiguration()
        {
            // Put nodes in a circle
            double theta = 0;
            double radius = 240;
            double deltaT = 2 * Math.PI / theGraph.Vertices.Count;
            foreach (Vertex v in theGraph.Vertices) {
                Point pt = new Point(radius * Math.Cos(theta), radius * Math.Sin(theta));
                moveVAndItsEdgesTo(v, pt);
                theta += deltaT;
            }
        }

        #endregion

        #region Transforms for Pan and Zoom of playground

        private void MainGrid_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.RightButton == MouseButtonState.Pressed)
            {
                mouseLastSeenAt = e.GetPosition(outerPanel);
            }
        }

        private void MainGrid_MouseMove(object sender, MouseEventArgs e)
        {
            if (e.RightButton == MouseButtonState.Pressed)
            {
                Point mouseNowAt = e.GetPosition(outerPanel);
                double dx = (mouseNowAt.X - mouseLastSeenAt.X);// / scale;
                double dy = (mouseNowAt.Y - mouseLastSeenAt.Y);// / scale;
                mouseLastSeenAt = mouseNowAt;
                playgroundTransform.Translate(dx, dy);
                playground.RenderTransform = new MatrixTransform(playgroundTransform);
                e.Handled = true;
            }
        }

        private void Window_MouseWheel(object sender, MouseWheelEventArgs e)
        {
            Point mPos = e.GetPosition(outerPanel);
            double scale = e.Delta < 0 ? 0.9 : 1.1;   // Zoom in or out
            playgroundTransform.ScaleAt(scale, scale, mPos.X, mPos.Y);
            playground.RenderTransform = new MatrixTransform(playgroundTransform);
        }

        #endregion

        #region Mouse drags, highlighting, etc on Vertices.
        // Entering a vertex brings it uppermost by moving it to the end of the children list.

        private void V_MouseMove(object sender, MouseEventArgs e)
        {
            if (e.LeftButton != MouseButtonState.Pressed) return;
            if (vertexBeingDragged == null) return;
            Shape v = sender as Shape;
            if (v == null) return;
            Point pos = e.GetPosition(playground);
            moveVAndItsEdgesTo(v.Tag as Vertex, pos);
            e.Handled = true;
        }

        private void V_MouseUp(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton != MouseButton.Left) return;
            if (vertexBeingDragged == null) return;
            vertexBeingDragged.ReleaseMouseCapture();
            vertexBeingDragged = null;
            e.Handled = true;
        }

        private void V_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton != MouseButton.Left) return;
            Shape v = sender as Shape;
            bool success = v.CaptureMouse();
            if (!success) return;
            vertexBeingDragged = v;
            e.Handled = true;
        }

        private void V_MouseLeave(object sender, MouseEventArgs e)
        {
            Shape v = sender as Shape;
            hightlightVertexAndEdges(v.Tag as Vertex, false);
        }

        private void V_MouseEnter(object sender, MouseEventArgs e)
        {
            Shape v = sender as Shape;
            hightlightVertexAndEdges(v.Tag as Vertex, true);
        }

        private void hightlightVertexAndEdges(Vertex vert, bool wantHighlight)
        {
            if (wantHighlight)
            {
                vert.UIWidget.Fill = VHighlightFill;
                playground.Children.Remove(vert.UIWidget);  // Promote V to topmost 
                playground.Children.Add(vert.UIWidget);
            }
            else
            {
                vert.UIWidget.Fill = VNormalFill;
            }

            Brush b = wantHighlight ? EStrokeHighlight : EStroke;
            foreach (Edge edge in theGraph.findOutEdges(vert))
            {
                edge.UIEdge.Stroke = b;
            }
            foreach (Edge edge in theGraph.findInEdges(vert))
            {
                edge.UIEdge.Stroke = b;
            }
        }

        private void moveVAndItsEdgesTo(Vertex vert, Point pos)
        {
            Canvas.SetLeft(vert.UIWidget, pos.X - HalfVSz);
            Canvas.SetTop(vert.UIWidget, pos.Y - HalfVSz);
            foreach (Edge edge in theGraph.findOutEdges(vert))
            {
                edge.UIEdge.X1 = pos.X;
                edge.UIEdge.Y1 = pos.Y;
            }
            foreach (Edge edge in theGraph.findInEdges(vert))
            {
                edge.UIEdge.X2 = pos.X;
                edge.UIEdge.Y2 = pos.Y;
            }
            int n = theGraph.CountCrossingEdges();
            numCrossings.Content = $"Intersections {n}";
            if (n == 0)
            {
                outerPanel.Background = SolvedBrush;
                tickTocker.IsEnabled = false;
            }
            else
            {
                outerPanel.Background = UnsolvedBrush;
            }
        }

        #endregion

    }
}
