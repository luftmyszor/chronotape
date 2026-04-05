using System;
using System.ComponentModel.Design;
using Phys;

const double DISPLAYED_WIDTH = 70;
const double DISPLAYED_HEIGHT = 70;
const double DISPLAYED_SEGMENT_CENTER_DISTANCE = 90;

const double SLIT_WIDTH = 20;
const double SLIT_HEIGHT = 20;
const double SLIT_SEGMENT_CENTER_DISTANCE = 50;

const int SLIT_AMOUNT = 4;

const double TAPE_TOP_HEIGHT_FROM_GROUND = 100;
/*
 *          +Y (Up)
 *             ^
 *             |
 *             |
 *             |
 *   -X <------+------> +X (Right)
 *             | (0,0,0)
 *             |
 *             |
 *             v
 *           -Y
 *
 */

Point3D chronotapeFrameOrigin = new Point3D(0, 0, 0);
Vector3D slitFramesDirection = new Vector3D(1, 0, 0);
Vector3D slitFramesUp = new Vector3D(0, 1, 0);
Vector3D slitFrameNormal = new Vector3D(0, 0, 1);

Vector3D surfaceNormal = new Vector3D(0, 0, 1);
Point3D surfacePoint = new Point3D(0, 0, 3000);
Plane displaySurface = new Plane(surfaceNormal, surfacePoint);

var slits = new List<Frame>();
// Calculate the exact middle index of the slit sequence
// For 4 slits, this is (4 - 1) / 2.0 = 1.5
double middleIndex = (SLIT_AMOUNT - 1) / 2.0;

for (int i = 0; i < SLIT_AMOUNT; i++)
{
    double currentOffset = (i - middleIndex) * SLIT_SEGMENT_CENTER_DISTANCE;

    Point3D slitCenter = new Point3D(
        chronotapeFrameOrigin.X + (slitFramesDirection.X * currentOffset),

        chronotapeFrameOrigin.Y + (slitFramesDirection.Y * currentOffset),

        chronotapeFrameOrigin.Z + (slitFramesDirection.Z * currentOffset)
        + TAPE_TOP_HEIGHT_FROM_GROUND
    );

    // 4. Create and add the frame
    Vector3D slitFrameUp = new Vector3D(0, 1, 0);

    Frame newSlit = new Frame(
        slitCenter,
        slitFrameNormal,
        slitFrameUp,
        SLIT_WIDTH,
        SLIT_HEIGHT
    );

    slits.Add(newSlit);
}

// --- Quick test ---
foreach (var slit in slits)
{
    Console.WriteLine($"X: {slit.Center.X} Y: {slit.Center.Y}  Z: {slit.Center.Z} ");
}

// --- Ceiling Displayed Segments Setup ---

var displayedSegments = new List<Frame>();

for (int i = 0; i < SLIT_AMOUNT; i++)
{
    double currentOffset = (i - middleIndex) * DISPLAYED_SEGMENT_CENTER_DISTANCE;

    // 2. Find the "shadow" of this segment on the flat floor
    Point3D groundPoint = new Point3D(
        chronotapeFrameOrigin.X + (slitFramesDirection.X * currentOffset),
        chronotapeFrameOrigin.Y,
        chronotapeFrameOrigin.Z + (slitFramesDirection.Z * currentOffset)
    );

    // 3. Project a ray from the ground point, through the corresponding slit center,
    //    onto the display surface. This works for any surface orientation because
    //    the ray travels along the tape's normal (Z) axis toward the display.
    if (!GeometryMath.GetProjectionPoint(groundPoint, slits[i].Center, displaySurface, out Point3D segmentCenter))
    {
        Console.WriteLine($"Warning: Could not project slit {i} onto display surface (ray is parallel to surface).");
        continue;
    }

    // 4. Calculate the perfect "Up" direction for the frame so it lies flat on the surface.
    // By crossing the Plane's Normal with the Track's Direction, it aligns perfectly to the track.
    Vector3D surfaceUp = new Vector3D(
        (displaySurface.Normal.Y * slitFramesDirection.Z) - (displaySurface.Normal.Z * slitFramesDirection.Y),
        (displaySurface.Normal.Z * slitFramesDirection.X) - (displaySurface.Normal.X * slitFramesDirection.Z),
        (displaySurface.Normal.X * slitFramesDirection.Y) - (displaySurface.Normal.Y * slitFramesDirection.X)
    );

    // 5. Create and add the frame
    Frame newSegment = new Frame(
        segmentCenter,
        displaySurface.Normal, // Faces the exact direction of the arbitrary plane
        surfaceUp,             // Lies flat on the plane, following the track
        DISPLAYED_WIDTH,
        DISPLAYED_HEIGHT
    );

    displayedSegments.Add(newSegment);
}

// --- Quick test for Ceiling Segments ---
Console.WriteLine("\n--- Displayed Segments (Any Surface) ---");
foreach (var segment in displayedSegments)
{
    Console.WriteLine($"X: {segment.Center.X}  Y: {segment.Center.Y}  Z: {segment.Center.Z}");
}