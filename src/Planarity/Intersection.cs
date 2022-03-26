using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Windows;

namespace Planarity
{

    public class Intersection
    {
        public LineEquation A { get; }
        public LineEquation B { get; }

        public Point Pt { get; }

        public List<Intersection> NeighbourIntersections = new List<Intersection>();

        public Intersection(LineEquation a, LineEquation b, Point p)
        {
            A = a; B = b; Pt = p;
        }

        internal void FindYourNeighbours(int k)
        {
            NeighbourIntersections.Clear();
            Intersection left = A.GetLeftNeighbour(k, Pt.X);
            if (left != null) { NeighbourIntersections.Add(left); }

            Intersection right = A.GetRightNeighbour(k, Pt.X);
            if (right != null) { NeighbourIntersections.Add(right); }

            left = B.GetLeftNeighbour(k, Pt.X);
            if (left != null) { NeighbourIntersections.Add(left); }

            right = B.GetRightNeighbour(k, Pt.X);
            if (right != null) { NeighbourIntersections.Add(right); }
        }


        static public List<Intersection> findIntersections(List<LineEquation> lines)
        {
            List<Intersection> crossings = new List<Intersection>();
            for (int i = 0; i < lines.Count - 1; i++)
            {
                for (int k = i + 1; k < lines.Count; k++)
                {
                    Intersection ins = lines[i].FindIntersection(lines[k]);
                    crossings.Add(ins);
                }
            }
            return crossings;
        }

        public static List<Intersection> generateIntersectionsFromRandomLines(Random rnd, int numVerts)
        {

            List<double> axs = new List<double>();
            for (double a = -80; a < 80; a += 7)
            {
                double rad = a / 360 * 2.0 * Math.PI;
                double slope = Math.Sin(a) / Math.Cos(a);
                axs.Add(slope);  // slope of possible lines, all different
            }

            // Generate graph with too many vertices to cater for case when
            // we might deliberately have same points of intersection.
            // Once we have a too-big planar graph we'll throw excess nodes away
            int numLines = 2;
            while (numLines * (numLines - 1) < numVerts * 2)
            {
                numLines++;
            }

            List<LineEquation> lines = new List<LineEquation>();
            for (int i = 0; i < numLines; i++)
            {
                // Pick a random slope, but remove it from future possible choices.
                int indx = rnd.Next(axs.Count);
                double a = axs[indx];
                axs.RemoveAt(indx);  // so we won't ever get parallel lines
                // Pick a random intercept b  
                double b = rnd.Next(1000) - 500;
                // Occasionally replace b with something we have already used, to get high-degree crossings
                //if (rnd.Next(100) > 30 && lines.Count > 0)
                //{
                //    int ix = rnd.Next(lines.Count);
                //    b = lines[ix].b;
                //}
                lines.Add(new LineEquation(a, b));

            }

            List<Intersection> crossings = Intersection.findIntersections(lines);
            foreach (LineEquation le in lines)
            {
                le.TrimXs();
            }
            for (int k = 0; k < crossings.Count; k++)
            {
                crossings[k].FindYourNeighbours(k);
            }
            //this.Title = $"N={numVerts}, lines={numLines},  Crossings = {crossings.Count}   ";
            //showLinesAndIntersections();
            return crossings;
        }


        //void showLinesAndIntersections(Canvas playground)
        //{
        //    //playground.Children.Clear();
        //    //showOrigin();
        //    //foreach (var le in lines)
        //    //{
        //    //    Line ln = new Line() { X1 = le.X0, Y1 = le.Y(le.X0), X2 = le.X1, Y2 = le.Y(le.X1), Stroke = Brushes.Blue };
        //    //    playground.Children.Add(ln);
        //    //}
        //    //foreach (Intersection ins in crossings)
        //    //{
        //    //    putMarkerAt(playground, ins.Pt, Brushes.Magenta, 6);
        //    //}
        //}

        //private void putMarkerAt(Canvas playground, Point pt, Brush b, double sz)
        //{
        //    double halfSz = sz / 2.0;
        //    Ellipse e = new Ellipse() { Fill = b, Stroke = VStroke, Width = sz, Height = sz };
        //    Canvas.SetLeft(e, pt.X - halfSz);
        //    Canvas.SetTop(e, pt.Y - halfSz);
        //    playground.Children.Add(e);
        //}



        public class LineEquation  // y = ax + b
        {
            public double a { get; }
            public double b { get; }

            public double X0 { get; set; }
            public double X1 { get; set; }

            bool isOrdered = false;

            public List<Intersection> crossings = new List<Intersection>();

            private void sortCrossings()
            {
                crossings.Sort((Intersection u, Intersection v) => { return u.Pt.X.CompareTo(v.Pt.X); });
                isOrdered = true;
            }
            public void TrimXs()
            {   // A lineEquation represents an infinite line, but I only want to render a bounded region of interest.
                sortCrossings();
                X0 = crossings[0].Pt.X;
                X1 = crossings[crossings.Count - 1].Pt.X;
            }

            public LineEquation(double slope, double intercept)
            {
                a = slope;
                b = intercept;
                X0 = -10000;
                X1 = 10000;
            }

            public double Y(double x)
            {
                return a * x + b;
            }

            public Intersection FindIntersection(LineEquation other)
            {
                double x = (other.b - b) / (a - other.a);
                Point p = new Point(x, Y(x));
                Intersection ins = new Intersection(this, other, p);
                crossings.Add(ins);
                other.crossings.Add(ins);
                return ins;
            }

            internal Intersection GetLeftNeighbour(int k, double x)
            {
                Debug.Assert(isOrdered);
                for (int i = 0; i < crossings.Count; i++)
                {
                    if (crossings[i].Pt.X == x)
                    {
                        if (i > 0) return crossings[i - 1];
                        //  parent.sayLn($"{k} No Left before {(int)x}");
                        return null;
                    }
                }
                //  parent.sayLn($"{k} No Left off end at {(int)x}");
                return null;
            }

            internal Intersection GetRightNeighbour(int k, double x)
            {
                Debug.Assert(isOrdered);
                for (int i = 0; i < crossings.Count; i++)
                {
                    if (crossings[i].Pt.X > x)
                    {
                        return crossings[i];
                    }
                }
                //  parent.sayLn($"{k} No Right after {(int)x}");
                return null;
            }
        }
    }
}