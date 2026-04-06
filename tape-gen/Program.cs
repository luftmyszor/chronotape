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
const double LIGHT_SOURCE_Z = 0;
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

// --- Displayed Segments Setup ---
// Frames are defined directly on the display surface without raycasting.

var displayedSegments = new List<Frame>();

// Calculate the "Up" direction so each frame lies flat on the display surface,
// aligned with the tape's direction. This is constant for all frames because
// the display surface normal and tape direction are the same for every frame.
Vector3D surfaceUp = Vector3D.Cross(displaySurface.Normal, slitFramesDirection);

for (int i = 0; i < SLIT_AMOUNT; i++)
{
    double currentOffset = (i - middleIndex) * DISPLAYED_SEGMENT_CENTER_DISTANCE;

    // Place the frame center directly on the display surface.
    Point3D segmentCenter = new Point3D(
        surfacePoint.X + (slitFramesDirection.X * currentOffset),
        surfacePoint.Y + (slitFramesDirection.Y * currentOffset),
        surfacePoint.Z + (slitFramesDirection.Z * currentOffset)
    );

    Frame newSegment = new Frame(
        segmentCenter,
        displaySurface.Normal,
        surfaceUp,
        DISPLAYED_WIDTH,
        DISPLAYED_HEIGHT
    );

    displayedSegments.Add(newSegment);
}

Console.WriteLine("\n--- Displayed Segments ---");
foreach (var segment in displayedSegments)
{
    Console.WriteLine($"X: {segment.Center.X}  Y: {segment.Center.Y}  Z: {segment.Center.Z}");
}

// --- Light Source Calculation ---
// For each tape frame, cast rays from the corresponding display frame's corners
// through the tape frame's corners to determine where the light source should be.

Plane lightSourcePlane = new Plane(new Vector3D(0, 0, 1), new Point3D(0, 0, LIGHT_SOURCE_Z));
var lightSources = new List<Point3D>();

for (int i = 0; i < SLIT_AMOUNT; i++)
{
    Frame displayFrame = displayedSegments[i];
    Frame tapeFrame = slits[i];

    if (!GeometryMath.GetProjectionPoint(displayFrame.TopLeft, tapeFrame.TopLeft, lightSourcePlane, out Point3D tlSource) ||
        !GeometryMath.GetProjectionPoint(displayFrame.TopRight, tapeFrame.TopRight, lightSourcePlane, out Point3D trSource) ||
        !GeometryMath.GetProjectionPoint(displayFrame.BottomLeft, tapeFrame.BottomLeft, lightSourcePlane, out Point3D blSource) ||
        !GeometryMath.GetProjectionPoint(displayFrame.BottomRight, tapeFrame.BottomRight, lightSourcePlane, out Point3D brSource))
    {
        Console.WriteLine($"Warning: Cannot determine light source for tape frame {i}.");
        continue;
    }

    // Average the 4 intersection points. For a perfect point-source projection the
    // four rays converge at the same spot; averaging handles any floating-point drift
    // and keeps the code correct for non-axis-aligned source planes.
    Point3D lightSource = new Point3D(
        (tlSource.X + trSource.X + blSource.X + brSource.X) / 4,
        (tlSource.Y + trSource.Y + blSource.Y + brSource.Y) / 4,
        (tlSource.Z + trSource.Z + blSource.Z + brSource.Z) / 4
    );

    lightSources.Add(lightSource);
}

Console.WriteLine("\n--- Light Sources ---");
foreach (var ls in lightSources)
{
    Console.WriteLine($"X: {ls.X}  Y: {ls.Y}  Z: {ls.Z}");
}