﻿using Helion.BSP.Geometry;
using Helion.Util.Geometry;

namespace Helion.BSP.States.Convex
{
    public enum ConvexState
    {
        NotStarted,
        Loaded,
        Traversing,
        FinishedIsDegenerate,
        FinishedIsConvex,
        FinishedIsSplittable,
    }

    public class ConvexStates
    {
        public ConvexState State = ConvexState.NotStarted;
        public ConvexTraversal ConvexTraversal = new ConvexTraversal();
        public BspSegment StartSegment = null;
        public BspSegment CurrentSegment = null;
        public Endpoint CurrentEndpoint = Endpoint.Start;
        public Rotation Rotation = Rotation.On;
        public int SegsVisited = 0;
        public int TotalSegs = 0;
    }
}
