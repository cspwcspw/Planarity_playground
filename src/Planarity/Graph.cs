using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.Windows;
using System.Windows.Shapes;

// Written by Pete, March/April 2022.

namespace Planarity
{

    // My initial planar graphs are generated more or less following John Tantalo's original method.
    // Create random non-parallel lines in the plane, find all intersections. These become the vertices in the graph.
    // Each line segment between two intersections becomes an edge in a planar graph.
    // Every infinite line intersects every other one. For n lines, I get (n*(n-1)/2) intersections which become vertices.
    // So if I'm trying to create a graph with V vertices, I chose the smallest n such that n*(n-1)/2 >= V.
    // This generally yields more vertices that I want.
    // 
    // A planar graph remains planar if I repeatedly collapse any two adjacent vertices into one,
    // so I randomly do this until I get the number of vertices I really want. 

    public class Graph
    {
        public List<Vertex> Vertices { get; private set; }

        public List<Edge> Edges { get; private set; }

        MainWindow parent;

        int numVerticesWanted;
        int seedUsed;
        Random rnd;   // We will own and control the random generator for test repeatability purposes

        public Graph(MainWindow parent, int numVerticesWanted)
        {
            this.parent = parent;
            // Double-stage the setup of rnd so that I can accurately reproduce situations of interest.
            //numVerticesWanted = 7;
            Random r2 = new Random();
            seedUsed = r2.Next(100);
            //seedUsed = 45;
            //numVerticesWanted = 6;
            this.numVerticesWanted = numVerticesWanted;


            rnd = new Random(seedUsed);
            List<Intersection> crossings = Intersection.generateIntersectionsFromRandomLines(rnd, numVerticesWanted);

            Vertices = new List<Vertex>();
            Edges = new List<Edge>();
            for (int i = 0; i < crossings.Count; i++)
            {
                Vertex vx = new Vertex(Vertices.Count);
                vx.UIWidget = parent.MakeNodeWidget(vx);
                Vertices.Add(vx);        
            }

            for (int i = 0; i < crossings.Count; i++)
            {
                foreach (Intersection ins in crossings[i].NeighbourIntersections)
                {
                    int k = crossings.IndexOf(ins);

                    Debug.Assert(k >= 0);
                    if (i < k)
                    {
                        Edge e = new Edge(Vertices[i], Vertices[k], Edges.Count);
                        e.UIEdge = parent.MakeEdgeWidget(e);
                        Edges.Add(e);
                       
                    }
                }
            }

            parent.say(Dump());
            CollapseGraphDownTo(numVerticesWanted);
        }

        public int CountCrossingEdges()
        {
            int n = 0;
            for (int i = 0; i < Edges.Count; i++)
            {
                for (int j = i + 1; j < Edges.Count; j++)
                {
                    if (Edges[i].DoWidgetsIntersect(Edges[j]))
                    {
                        n++;
                    }
                }
            }
            return n;
        }

        public void CollapseGraphDownTo(int numVerticesWanted)
        {
            while (Vertices.Count > numVerticesWanted)
            {
                Tuple<Vertex, Vertex> pairToFold = findAdjacentVertexPair();
                combineNodes(pairToFold.Item1, pairToFold.Item2);
                parent.say(Dump());

                //if (hasNodeOfDegreeOne())
                //{
                //    parent.say($"Got case when reducing to {Vertices.Count}\n after folding {pairToFold} {pairToFold.Item1} & {pairToFold.Item2}\n");                
                //}
            }
        }

        //private bool hasNodeOfDegreeOne()
        //{
        //    foreach (Vertex v in Vertices)
        //    {
        //        List<Edge> inEs = findInEdges(v);
        //        List<Edge> outEs = findOutEdges(v);
        //        if (inEs.Count + outEs.Count == 1)
        //        {
        //            parent.say($"Problem vertex is {v}\n");
        //            return true;
        //        }
        //    }

        //    return false;
        //}

        private void combineNodes(Vertex me, Vertex other)
        {
            parent.say($"Folding {other.vNum} into {me.vNum}\n");


            // First, remove the edge from me to the node I am absorbing
            //  var before = findInEdges(other);
            int indx = Edges.FindIndex((Edge oe) => { return oe.VFrom == me && oe.VTo == other; });
            Edges.RemoveAt(indx);
            //  var after = findInEdges(other);

            // I must take ownership of other's outEdges as my own, or kill the edge if I already have an outedge to that node
            foreach (Edge e in findOutEdges(other))
            {
                Debug.Assert(me.vNum < e.VTo.vNum);
                if (haveOutEdgeTo(me, e.VTo))
                {
                    Edges.Remove(e);
                }
                else
                {
                    takeOverOtherOutEdge(e, me);
                }
            }

            foreach (Edge e in findInEdges(other))
            {
                Vertex otherSrc = e.VFrom;
                if (otherSrc == me)
                {
                    string msg = $"Problem case: seedUsed={seedUsed}, numverticesWanted={numVerticesWanted} Vertices.Count={Vertices.Count}\n";
                    parent.say(msg);
                    MessageBox.Show(msg);
                    Debug.Assert(false);
                }
                else
                {
                    if (me.vNum < otherSrc.vNum)
                    {

                        //if (other.vNum == 7)
                        //{
                        //    string s = "Problem";
                        //}

                        if (haveOutEdgeTo(me, e.VFrom))
                        {
                            Edges.Remove(e);
                        }
                        else
                        {
                            takeOverOtherInEdge(e, me);
                        }
                    }
                    else
                    {
                        if (haveInEdgeFrom(me, otherSrc))
                        {
                            Edges.Remove(e);
                        }
                        else
                        {
                            takeOverOtherInEdge(e, me);
                        }
                    }
                }
            }
            Vertices.Remove(other);
        }

        private bool haveInEdgeFrom(Vertex me, Vertex otherSrc)
        {
            Debug.Assert(otherSrc.vNum < me.vNum);
            int pos = Edges.FindIndex((Edge oe) => { return oe.VFrom == otherSrc && oe.VTo == me; });
            return pos >= 0;
        }

        private bool haveOutEdgeTo(Vertex me, Vertex other)
        {
            Debug.Assert(me.vNum < other.vNum);  // Redundant
            int pos = Edges.FindIndex((Edge oe) => { return oe.VFrom == me && oe.VTo == other; });
            return pos >= 0;
        }

        private void takeOverOtherOutEdge(Edge e, Vertex me)
        {
            // I'm keeping edges s with VFrom < VTo, so swap things around if needed
            if (me.vNum < e.VFrom.vNum)
            {
                e.VFrom = me;
            }
            else
            {
                e.VTo = e.VFrom;
                e.VTo = me;
                // Swap line endpoints
            }
            Debug.Assert(e.VFrom.vNum < e.VTo.vNum);
        }


        private void takeOverOtherInEdge(Edge e, Vertex me)
        {
            // I'm keeping edges s with VFrom < VTo, so swap things around if needed
            if (me.vNum < e.VFrom.vNum)
            {
                e.VTo = e.VFrom;
                e.VFrom = me;
            }
            else
            {
                e.VTo = me;
            }
            Debug.Assert(e.VFrom.vNum < e.VTo.vNum);
        }


        private Tuple<Vertex, Vertex> findAdjacentVertexPair()
        {
            int p = rnd.Next(Vertices.Count);
            while (true)
            {
                Vertex v1 = Vertices[p];
                Edge? e = Edges.Find((Edge oe) => { return oe.VFrom == v1; });
                if (e != null)
                {
                    return new Tuple<Vertex, Vertex>(v1, e.VTo);
                }
                p = (p + 1) % Vertices.Count;
            }
        }

        //private void AddEdge(Vertex v0, Vertex v1)
        //{
        //    int edgeNum = Edges.Count;
        //    Edge e = new Edge(v0, v1, edgeNum);
        //    Edges.Add(e);
        //    parent.AddWidget(e, v0, v1);
        //}

        //private void MakeVertex()
        //{
        //    int n = Vertices.Count;
        //    Vertex vx = new Vertex(n);
        //    Vertices.Add(vx);
        //    parent.AddWidget(vx, n);
        //}

        public List<Edge> findOutEdges(Vertex v)
        {
            List<Edge> edges = Edges.FindAll((Edge oe) => { return oe.VFrom == v; });
            return edges;
        }

        public List<Edge> findInEdges(Vertex v)
        {
            List<Edge> edges = Edges.FindAll((Edge oe) => { return oe.VTo == v; });
            return edges;
        }

        public string Dump()
        {
            StringBuilder sb = new StringBuilder();
            sb.Append("-----\n");
            foreach (Vertex v in Vertices)
            {
                sb.Append(v.ToString());
                foreach (Edge e in findOutEdges(v))
                {
                    sb.Append(e.ToString());
                }

                sb.AppendLine();
            }
            sb.AppendLine("++++");
            return sb.ToString();
        }
    }

    public class Vertex
    {
        public Shape UIWidget { get; set; } = null;           // Initially null, Main will store the UI elem here
        public int vNum { get; set; }

        public Vertex(int vNum)
        {
            this.vNum = vNum;
        }

        public override string ToString()
        {
            return $"Vertex {vNum}";
        }
    }

    public class Edge
    {
        public Line UIEdge { get; set; } = null;         // Initially null, Main will store the UI elem here
        public Vertex VFrom { get; set; }
        public Vertex VTo { get; set; }

        public int eNum { get; set; }
        public Edge(Vertex v1, Vertex v2, int eNum)
        {
            this.VFrom = v1;
            this.VTo = v2;
            this.eNum = eNum;
        }

        public override string ToString()
        {
            return $" E({eNum},{VFrom.vNum}->{VTo.vNum})";
        }

        // Return True if my UIEdge intersects the other UIEdge
        internal bool DoWidgetsIntersect(Edge other)
        {
            // For planarity, any edges that share a same vertex endpoint are considered non-overlapping at the shared point.
            if (VFrom == other.VFrom || VTo == other.VFrom || VFrom == other.VTo || VTo == other.VTo) return false;

            //   https://www.codeproject.com/Tips/862988/Find-the-Intersection-Point-of-Two-Line-Segments 
            Vector intersection;
            bool result = Helpers.LineSegementsIntersect(
                new Vector(UIEdge.X1, UIEdge.Y1),
                new Vector(UIEdge.X2, UIEdge.Y2),
                new Vector(other.UIEdge.X1, other.UIEdge.Y1),
                new Vector(other.UIEdge.X2, other.UIEdge.Y2),
                out intersection);
            return result;
        }
    }
}
