using ObenFramework.SceneLook;

namespace ObenCore.Tests;

public class SceneLookMeshTests
{
    private static SceneLookChannel Channel(int stream, int offset, int format, int dimension) =>
        new() { Stream = stream, Offset = offset, Format = format, Dimension = dimension };

    // Position and normal (float3 each) in stream 0, UV (float2) in stream 1, three vertices.
    private static SceneLookMesh TwoStreams() => new()
    {
        VertexCount = 3,
        Channels = new List<SceneLookChannel> { Channel(0, 0, 0, 3), Channel(0, 12, 0, 3), Channel(0, 0, 0, 0), Channel(0, 0, 0, 0), Channel(1, 0, 0, 2) },
        VertexDataSize = 80 + 24,
    };

    [Fact]
    public void Streams_follow_each_other_padded_to_sixteen_bytes()
    {
        var mesh = TwoStreams();
        var streams = SceneLookMeshPlan.Plan(mesh, out string reason);

        Assert.Null(reason);
        Assert.Equal(2, streams.Count);
        Assert.Equal(24, streams[0].Stride);
        Assert.Equal(80, streams[1].Start);
        Assert.Equal(8, streams[1].Stride);
        Assert.Equal(new[] { 0, 1, 4 }, SceneLookMeshPlan.Attributes(mesh));
        Assert.Equal(104, SceneLookMeshReader.ExpectedVertexBytes(mesh));
    }

    [Fact]
    public void Layouts_that_differ_from_what_unity_writes_are_refused()
    {
        var gap = TwoStreams();
        gap.Channels[1] = Channel(0, 16, 0, 3);
        Assert.Null(SceneLookMeshPlan.Plan(gap, out string offset));
        Assert.Contains("offsets", offset);

        var size = TwoStreams();
        size.VertexDataSize = 100;
        Assert.Null(SceneLookMeshPlan.Plan(size, out string bytes));
        Assert.Contains("layout needs 104", bytes);

        var format = TwoStreams();
        format.Channels[0] = Channel(0, 0, 12, 3);
        Assert.Null(SceneLookMeshPlan.Plan(format, out string handled));
        Assert.Contains("not handled", handled);
    }

    [Fact]
    public void Mesh_reasons_tell_streamed_and_broken_buffers()
    {
        var mesh = TwoStreams();
        mesh.Submeshes = new List<SceneLookSubmesh> { new() { IndexCount = 3 } };
        mesh.ExpectedVertexDataSize = 104;
        mesh.IndexFormat = 0;
        mesh.IndexDataSize = 6;
        Assert.Empty(SceneLookMeshReader.MeshReasons(mesh));

        mesh.VertexDataSize = 0;
        Assert.Equal(new[] { "mesh vertex data not inline" }, SceneLookMeshReader.MeshReasons(mesh));
        mesh.VertexDataSize = 104;
        mesh.IndexDataSize = 12;
        Assert.Equal(new[] { "mesh index size mismatch" }, SceneLookMeshReader.MeshReasons(mesh));
    }

    // Two vertices; one blend shape "Smile" with two frames that move vertex 1 up (and its normal in the second frame).
    private static SceneLookMesh Shaped()
    {
        var data = new ObjectBytes()
            .Floats(0, 0.5f, 0).Floats(0, 0, 0).Floats(0, 0, 0).U32(1)
            .Floats(0, 1f, 0).Floats(0, 0, 1).Floats(0, 0, 0).U32(1)
            .ToArray();
        return new SceneLookMesh
        {
            VertexCount = 2,
            BlendShapeVertexCount = 2,
            BlendShapeVertexData = data,
            BlendShapes = new List<SceneLookBlendShape>
            {
                new() { FirstVertex = 0, VertexCount = 1 },
                new() { FirstVertex = 1, VertexCount = 1, HasNormals = true },
            },
            BlendShapeNames = new List<string> { "Smile" },
            BlendShapeChannels = new List<SceneLookBlendShapeChannel> { new() { FirstFrame = 0, Frames = 2 } },
            BlendShapeFrameWeights = new[] { 50f, 100f },
        };
    }

    [Fact]
    public void Blend_shape_frames_come_out_in_channel_order_with_their_weights()
    {
        var frames = SceneLookBlendShapes.Frames(Shaped(), out string reason);

        Assert.Null(reason);
        Assert.Equal(2, frames.Count);
        Assert.All(frames, f => Assert.Equal("Smile", f.Name));
        Assert.Equal(new[] { 50f, 100f }, frames.Select(f => f.Weight));
        Assert.True(frames[1].HasNormals);
    }

    [Fact]
    public void Blend_shape_deltas_land_on_their_vertex()
    {
        var mesh = Shaped();
        var frames = SceneLookBlendShapes.Frames(mesh, out _);
        var positions = new float[6];
        var normals = new float[6];
        var tangents = new float[6];

        Assert.True(SceneLookBlendShapes.Fill(mesh, frames[1], positions, normals, tangents));

        Assert.Equal(new[] { 0f, 0f, 0f, 0f, 1f, 0f }, positions);
        Assert.Equal(1f, normals[5]);
        Assert.All(tangents, t => Assert.Equal(0f, t));
    }

    [Fact]
    public void Broken_blend_shapes_are_refused()
    {
        var missing = Shaped();
        missing.BlendShapeVertexData = new byte[10];
        Assert.Null(SceneLookBlendShapes.Frames(missing, out string notLoaded));
        Assert.Contains("not loaded", notLoaded);

        var past = Shaped();
        past.BlendShapeChannels[0].Frames = 3;
        Assert.Null(SceneLookBlendShapes.Frames(past, out string frames));
        Assert.Contains("past the list", frames);

        var outside = Shaped();
        outside.VertexCount = 1;
        var list = SceneLookBlendShapes.Frames(outside, out _);
        Assert.False(SceneLookBlendShapes.Fill(outside, list[0], new float[3], new float[3], new float[3]));

        Assert.Empty(SceneLookBlendShapes.Frames(new SceneLookMesh(), out _));
    }
}
