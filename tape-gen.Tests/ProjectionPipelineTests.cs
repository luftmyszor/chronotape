using Phys;
using Xunit;

public sealed class ProjectionPipelineTests
{
    [Fact]
    public void ProjectThroughSlitGlyphToDisplay_OriginPixelProjectsThroughSlitCenter()
    {
        Frame slit = new(
            new Point3D(0, 0, 0),
            new Vector3D(0, 0, 1),
            new Vector3D(0, 1, 0),
            width: 10,
            height: 10);
        Frame display = new(
            new Point3D(0, 0, 10),
            new Vector3D(0, 0, 1),
            new Vector3D(0, 1, 0),
            width: 40,
            height: 40);
        var pixels = new List<SampledPixel>
        {
            new SampledPixel { X = 0, Y = 0 }
        };

        SlitProjectionResult result = ProjectionPipeline.ProjectThroughSlitGlyphToDisplay(
            slitIndex: 0,
            sampledPixels: pixels,
            slit: slit,
            display: display,
            lightSource: new Point3D(0, 0, -10),
            pixelSizeMm: 1.0);

        Assert.Single(result.Points);
        ProjectedPoint point = result.Points[0];
        Assert.Equal(0, point.SlitLocalX, 6);
        Assert.Equal(0, point.SlitLocalY, 6);
        Assert.Equal(0, point.DisplayLocalX, 6);
        Assert.Equal(0, point.DisplayLocalY, 6);
        Assert.Equal(10, point.DisplayWorldZ, 6);
    }

    [Fact]
    public void ProjectThroughSlitGlyphToDisplay_PixelCoordinatesMapsDirectlyToSlitLocalWithNoScaling()
    {
        // Pixel (3, 5) at pixelSizeMm=1.0 → slit-local (3mm, -5mm).
        // Light at (0,0,-10), slit at origin (z=0), display at z=10.
        // Ray: from (0,0,-10) through (3,-5,0) → parametric t=2 → hits (6,-10,10).
        Frame slit = new(
            new Point3D(0, 0, 0),
            new Vector3D(0, 0, 1),
            new Vector3D(0, 1, 0),
            width: 100,
            height: 100);
        Frame display = new(
            new Point3D(0, 0, 10),
            new Vector3D(0, 0, 1),
            new Vector3D(0, 1, 0),
            width: 100,
            height: 100);
        var pixels = new List<SampledPixel>
        {
            new SampledPixel { X = 3, Y = 5 }
        };

        SlitProjectionResult result = ProjectionPipeline.ProjectThroughSlitGlyphToDisplay(
            slitIndex: 0,
            sampledPixels: pixels,
            slit: slit,
            display: display,
            lightSource: new Point3D(0, 0, -10),
            pixelSizeMm: 1.0);

        Assert.Single(result.Points);
        ProjectedPoint point = result.Points[0];
        Assert.Equal(3, point.SlitLocalX, 6);
        Assert.Equal(-5, point.SlitLocalY, 6);
        Assert.Equal(6, point.DisplayLocalX, 6);
        Assert.Equal(-10, point.DisplayLocalY, 6);
    }

    [Fact]
    public void ProjectThroughSlitGlyphToDisplay_PixelSizeMmScalesSlitLocalCoordinates()
    {
        // Same pixel but different pixelSizeMm should produce proportionally different slit-local coords.
        Frame slit = new(
            new Point3D(0, 0, 0),
            new Vector3D(0, 0, 1),
            new Vector3D(0, 1, 0),
            width: 100,
            height: 100);
        Frame display = new(
            new Point3D(0, 0, 10),
            new Vector3D(0, 0, 1),
            new Vector3D(0, 1, 0),
            width: 100,
            height: 100);
        var pixels = new List<SampledPixel>
        {
            new SampledPixel { X = 10, Y = 20 }
        };

        SlitProjectionResult result1 = ProjectionPipeline.ProjectThroughSlitGlyphToDisplay(
            slitIndex: 0, sampledPixels: pixels, slit: slit, display: display,
            lightSource: new Point3D(0, 0, -10), pixelSizeMm: 0.5);
        SlitProjectionResult result2 = ProjectionPipeline.ProjectThroughSlitGlyphToDisplay(
            slitIndex: 0, sampledPixels: pixels, slit: slit, display: display,
            lightSource: new Point3D(0, 0, -10), pixelSizeMm: 0.1);

        Assert.Single(result1.Points);
        Assert.Single(result2.Points);
        Assert.Equal(5.0, result1.Points[0].SlitLocalX, 6);
        Assert.Equal(-10.0, result1.Points[0].SlitLocalY, 6);
        Assert.Equal(1.0, result2.Points[0].SlitLocalX, 6);
        Assert.Equal(-2.0, result2.Points[0].SlitLocalY, 6);
    }

    [Fact]
    public void ProjectThroughSlitGlyphToDisplay_BitmapDimensionsDoNotAffectMapping()
    {
        Frame slit = new(
            new Point3D(0, 0, 0),
            new Vector3D(0, 0, 1),
            new Vector3D(0, 1, 0),
            width: 10,
            height: 10);
        Frame display = new(
            new Point3D(0, 0, 10),
            new Vector3D(0, 0, 1),
            new Vector3D(0, 1, 0),
            width: 40,
            height: 40);
        var pixelsSmall = new List<SampledPixel>
        {
            new SampledPixel { X = 2, Y = 3, BitmapWidth = 10, BitmapHeight = 10 }
        };
        var pixelsLarge = new List<SampledPixel>
        {
            new SampledPixel { X = 2, Y = 3, BitmapWidth = 500, BitmapHeight = 500 }
        };

        SlitProjectionResult resultSmall = ProjectionPipeline.ProjectThroughSlitGlyphToDisplay(
            slitIndex: 0,
            sampledPixels: pixelsSmall,
            slit: slit,
            display: display,
            lightSource: new Point3D(0, 0, -10),
            pixelSizeMm: 1.0);
        SlitProjectionResult resultLarge = ProjectionPipeline.ProjectThroughSlitGlyphToDisplay(
            slitIndex: 0,
            sampledPixels: pixelsLarge,
            slit: slit,
            display: display,
            lightSource: new Point3D(0, 0, -10),
            pixelSizeMm: 1.0);

        Assert.Single(resultSmall.Points);
        Assert.Single(resultLarge.Points);
        Assert.Equal(resultSmall.Points[0].SlitLocalX, resultLarge.Points[0].SlitLocalX, 6);
        Assert.Equal(resultSmall.Points[0].SlitLocalY, resultLarge.Points[0].SlitLocalY, 6);
        Assert.Equal(resultSmall.Points[0].DisplayLocalX, resultLarge.Points[0].DisplayLocalX, 6);
        Assert.Equal(resultSmall.Points[0].DisplayLocalY, resultLarge.Points[0].DisplayLocalY, 6);
    }
}
