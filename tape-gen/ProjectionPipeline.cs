using Phys;

internal static class ProjectionPipeline
{
    public static SlitProjectionResult ProjectSingleSlit(
        int slitIndex,
        IReadOnlyList<SampledPixel> sampledPixels,
        Frame display,
        Frame slit,
        Point3D lightSource)
    {
        Plane slitPlane = new Plane(FrameMath.GetFrameNormal(slit), slit.Center);

        Vector3D displayRight = FrameMath.GetFrameRight(display);
        Vector3D displayUp = FrameMath.GetFrameUp(display);
        double displayWidth = FrameMath.GetFrameWidth(display);
        double displayHeight = FrameMath.GetFrameHeight(display);

        Vector3D slitRight = FrameMath.GetFrameRight(slit);
        Vector3D slitUp = FrameMath.GetFrameUp(slit);
        double slitHalfWidth = FrameMath.GetFrameWidth(slit) / 2.0;
        double slitHalfHeight = FrameMath.GetFrameHeight(slit) / 2.0;

        var projectedPoints = new List<ProjectedPoint>();
        foreach (SampledPixel pixel in sampledPixels)
        {
            double u = (((pixel.X + 0.5) / pixel.BitmapWidth) - 0.5) * displayWidth;
            double v = (0.5 - ((pixel.Y + 0.5) / pixel.BitmapHeight)) * displayHeight;
            Point3D displayPoint = FrameMath.OffsetPoint(display.Center, displayRight, u, displayUp, v);

            if (!GeometryMath.GetProjectionPoint(lightSource, displayPoint, slitPlane, out Point3D intersection))
            {
                continue;
            }

            Vector3D centerToIntersection = new Vector3D(slit.Center, intersection);
            double localX = Vector3D.Dot(centerToIntersection, slitRight);
            double localY = Vector3D.Dot(centerToIntersection, slitUp);

            if (Math.Abs(localX) > slitHalfWidth || Math.Abs(localY) > slitHalfHeight)
            {
                continue;
            }

            projectedPoints.Add(new ProjectedPoint
            {
                PixelX = pixel.X,
                PixelY = pixel.Y,
                DisplayWorldX = displayPoint.X,
                DisplayWorldY = displayPoint.Y,
                DisplayWorldZ = displayPoint.Z,
                WorldX = intersection.X,
                WorldY = intersection.Y,
                WorldZ = intersection.Z,
                SlitLocalX = localX,
                SlitLocalY = localY
            });
        }

        return new SlitProjectionResult
        {
            SlitIndex = slitIndex,
            LightSource = lightSource,
            SampleCount = projectedPoints.Count,
            Points = projectedPoints
        };
    }

    public static bool[][] BuildSourceBitmap(int width, int height, IReadOnlyList<ProjectedPoint> points)
    {
        bool[][] bitmap = BuildEmptyBitmap(width, height);

        foreach (ProjectedPoint point in points)
        {
            if (point.PixelY < 0 || point.PixelY >= height || point.PixelX < 0 || point.PixelX >= width)
            {
                continue;
            }

            bitmap[point.PixelY][point.PixelX] = true;
        }

        return bitmap;
    }

    public static bool[][] BuildSlitLocalBitmap(int width, int height, Frame slitFrame, IReadOnlyList<ProjectedPoint> points)
    {
        bool[][] bitmap = BuildEmptyBitmap(width, height);
        double halfWidth = FrameMath.GetFrameWidth(slitFrame) / 2.0;
        double halfHeight = FrameMath.GetFrameHeight(slitFrame) / 2.0;
        if (halfWidth <= 0 || halfHeight <= 0)
        {
            return bitmap;
        }

        foreach (ProjectedPoint point in points)
        {
            double normalizedX = (point.SlitLocalX + halfWidth) / (2.0 * halfWidth);
            double normalizedY = (halfHeight - point.SlitLocalY) / (2.0 * halfHeight);
            int x = (int)Math.Floor(normalizedX * width);
            int y = (int)Math.Floor(normalizedY * height);

            if (x < 0 || x >= width || y < 0 || y >= height)
            {
                continue;
            }

            bitmap[y][x] = true;
        }

        return bitmap;
    }

    private static bool[][] BuildEmptyBitmap(int width, int height)
    {
        var bitmap = new bool[height][];
        for (int y = 0; y < height; y++)
        {
            bitmap[y] = new bool[width];
        }

        return bitmap;
    }
}
