﻿using Helion.BSP.Geometry;
using Helion.BSP.States;
using Helion.BSP.States.Convex;
using Helion.BSP.States.Miniseg;
using Helion.BSP.States.Partition;
using Helion.BSP.States.Split;
using Helion.Util.Geometry;
using System.Drawing;

namespace BspVisualizer
{
    public partial class Form1
    {
        /// <summary>
        /// Draws a segment on the canvas. This handles all of the transformations.
        /// </summary>
        /// <param name="g">The graphics to draw with.</param>
        /// <param name="pen">The pen to color the line with.</param>
        /// <param name="segment">The segment to draw.</param>
        private void DrawSegment(Graphics g, Pen pen, BspSegment segment)
        {
            Vec2I start = TransformPoint(segment.Start);
            Vec2I end = TransformPoint(segment.End);
            g.DrawLine(pen, start.X, start.Y, end.X, end.Y);
        }

        private void DrawVertex(Graphics g, Brush brush, Vec2D vertex)
        {
            Vec2I point = TransformPoint(vertex);

            // When we're zoomed out, points can become very dominant on the
            // screen, so we want smaller ones when zoomed out.
            if (zoom < 0.125f)
                g.FillRectangle(brush, new Rectangle(new Point(point.X, point.Y), new Size(1, 1)));
            else
            {
                Size size = (zoom < 0.25f ? new Size(2, 2) : new Size(3, 3));
                g.FillRectangle(brush, new Rectangle(new Point(point.X - 1, point.Y - 1), size));
            }
        }

        private void AddMajorStateInfo()
        {
            bottomLeftCornerDrawer.Add(
                Color.White, "State: ", 
                Color.Cyan, bspBuilder.States.Current.ToString()
            );
        }

        private void PaintBackground(Graphics g, Rectangle windowBounds)
        {
            g.FillRectangle(blackBackgroundBrush, windowBounds);
        }

        private void PaintShadowLines(Graphics g)
        {
            shadowSegments.ForEach(seg => DrawSegment(g, seg.OneSided ? shadowOneSidedLinePen : shadowTwoSidedLinePen, seg));
        }

        private void PaintCurrentWorkItem(Graphics g)
        {
            BspWorkItem? workItem = bspBuilder.GetCurrentWorkItem();
            if (workItem == null)
                return;

            foreach (BspSegment seg in workItem.Segments)
                DrawSegment(g, seg.OneSided ? whitePen : grayPen, seg);
        }

        private void PaintVertices(Graphics g)
        {
            foreach (Vec2D vertex in bspBuilder.VertexAllocator)
                DrawVertex(g, cyanBrush, vertex);
        }

        private void PaintTextInfo(Graphics g, Rectangle windowBounds)
        {
            bottomLeftCornerDrawer.DrawAndClear(g, windowBounds, defaultFont);
        }

        private void PaintCurrentState(Graphics g, Rectangle windowBounds)
        {
            switch (bspBuilder.States.Current)
            {
            case BuilderState.CheckingConvexity:
                DrawCheckingConvexity(g);
                break;
            case BuilderState.FindingSplitter:
                DrawFindingSplitter(g);
                break;
            case BuilderState.PartitioningSegments:
                DrawPartitioningSegments(g);
                break;
            case BuilderState.GeneratingMinisegs:
                DrawGeneratingMinisegs(g);
                break;
            case BuilderState.NotStarted:
            case BuilderState.CreatingLeafNode:
            case BuilderState.FinishingSplit:
            case BuilderState.Complete:
            default:
                break;
            }
        }

        private void DrawCheckingConvexity(Graphics g)
        {
            ConvexStates states = bspBuilder.ConvexChecker.States;

            if (states.StartSegment != null)
                DrawSegment(g, redPen, states.StartSegment);
            if (states.CurrentSegment != null)
                DrawSegment(g, cyanPen, states.CurrentSegment);

            bottomLeftCornerDrawer.Add(Color.White, "Substate: ", Color.Cyan, states.State.ToString());
            bottomLeftCornerDrawer.Add(Color.White, $"Visited {states.SegsVisited} / {states.TotalSegs} segs");
            bottomLeftCornerDrawer.Add(Color.White, "Rotation: ", Color.Aqua, states.Rotation.ToString());
        }

        private void DrawFindingSplitter(Graphics g)
        {
            SplitterStates states = bspBuilder.SplitCalculator.States;

            bottomLeftCornerDrawer.Add(Color.White, "Substate: ", Color.Cyan, states.State.ToString());

            if (states.BestSplitter != null)
            {
                bottomLeftCornerDrawer.Add(
                    Color.White, "Best segment score: ", 
                    Color.LightGreen, states.BestSegScore.ToString()
                );
                bottomLeftCornerDrawer.Add(
                    Color.White, "Best segment: ", 
                    Color.Cyan, states.BestSplitter.ToString()
                );

                DrawSegment(g, redPen, states.BestSplitter);
            }

            if (states.State != SplitterState.Loaded && states.CurrentSegmentIndex < states.Segments.Count)
            {
                BspSegment currentSegment = states.Segments[states.CurrentSegmentIndex];

                bottomLeftCornerDrawer.Add(
                    Color.White, "Current segment score: ", 
                    Color.LightGreen, states.CurrentSegScore.ToString()
                );
                bottomLeftCornerDrawer.Add(
                    Color.White, $"Current segment ({states.CurrentSegmentIndex} / {states.Segments.Count}): ",
                    Color.Cyan, currentSegment.ToString()
                );

                DrawSegment(g, cyanPen, currentSegment);
            }
        }

        private void DrawPartitioningSegments(Graphics g)
        {
            PartitionStates states = bspBuilder.Partitioner.States;

            bottomLeftCornerDrawer.Add(Color.White, "Substate: ", Color.Cyan, states.State.ToString());

            foreach (BspSegment leftSegment in states.LeftSegments)
                DrawSegment(g, bluePen, leftSegment);
            foreach (BspSegment rightSegment in states.RightSegments)
                DrawSegment(g, redPen, rightSegment);

            if (states.Splitter != null)
                DrawSegment(g, yellowPen, states.Splitter);

            if (states.CurrentSegToPartitionIndex < states.SegsToSplit.Count)
            {
                BspSegment currentSegment = states.SegsToSplit[states.CurrentSegToPartitionIndex];
                DrawSegment(g, cyanPen, currentSegment);

                bottomLeftCornerDrawer.Add(Color.White, $"Segment {states.CurrentSegToPartitionIndex} / {states.SegsToSplit.Count}");
            }
        }

        private void DrawGeneratingMinisegs(Graphics g)
        {
            MinisegStates states = bspBuilder.MinisegCreator.States;

            bottomLeftCornerDrawer.Add(
                Color.White, "Substate: ", 
                Color.Cyan, states.State.ToString()
            );
            bottomLeftCornerDrawer.Add(
                Color.White, "In void: ", 
                Color.LightGreen, states.VoidStatus.ToString()
            );

            foreach (BspSegment miniseg in states.Minisegs)
                DrawSegment(g, redPen, miniseg);

            if (states.CurrentVertexListIndex + 1 < states.Vertices.Count)
            {
                VertexSplitterTime firstVertexTime = states.Vertices[states.CurrentVertexListIndex];
                Vec2D firstVertex = bspBuilder.VertexAllocator[firstVertexTime.Index];

                VertexSplitterTime secondVertexTime = states.Vertices[states.CurrentVertexListIndex + 1];
                Vec2D secondVertex = bspBuilder.VertexAllocator[secondVertexTime.Index];

                // The corner drawers are a stack that builds upwards from the
                // bottom, so the order to drawing them is reversed.
                bottomLeftCornerDrawer.Add(
                    Color.White, "Second vertex: ",
                    Color.LightGreen, secondVertex.ToString(),
                    Color.White, "at t = ",
                    Color.Cyan, secondVertexTime.tSplitter.ToString()
                );

                bottomLeftCornerDrawer.Add(
                    Color.White, "First vertex: ",
                    Color.LightGreen, firstVertex.ToString(),
                    Color.White, "at t = ",
                    Color.Cyan, firstVertexTime.tSplitter.ToString()
                );
            }
        }
    }
}
