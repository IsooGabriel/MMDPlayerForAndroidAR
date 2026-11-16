using System.Threading.Tasks;
using MMDPlayerForVR.PmxImporter;
using MMDPlayerForVR.PmxImporter.Builders;
using MMDPlayerForVR.PmxImporter.Core;
using MMDPlayerForVR.PmxImporter.Parsers;
using MMDPlayerForVR.Pose.Application;
using NUnit.Framework;
using UnityEngine;

public class PmxImporterPipelineTests
{
    private GameObject _model;
    private PmxMaterialTemplates _templates;
    private Material _templateMaterial;

    [TearDown]
    public void TearDown()
    {
        if (_model != null)
        {
            SkinnedMeshRenderer renderer = _model.GetComponent<SkinnedMeshRenderer>();
            if (renderer != null)
            {
                if (renderer.sharedMesh != null)
                {
                    Object.DestroyImmediate(renderer.sharedMesh);
                }

                foreach (Material material in renderer.sharedMaterials)
                {
                    if (material != null)
                    {
                        Object.DestroyImmediate(material);
                    }
                }
            }

            Object.DestroyImmediate(_model);
        }

        if (_templateMaterial != null)
        {
            Object.DestroyImmediate(_templateMaterial);
        }

        if (_templates != null)
        {
            Object.DestroyImmediate(_templates);
        }
    }

    [Test]
    public async Task ImportAsync_MinimalPmx_BuildsModelAndSkinMesh()
    {
        byte[] bytes = TestPmxBuilder.CreateMinimal(deformType: PmxWeightDeformType.BDEF4);
        var reader = new InMemoryReader(bytes);
        Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
        Assert.That(shader, Is.Not.Null, "A material shader must be available in the Editor.");

        _templates = ScriptableObject.CreateInstance<PmxMaterialTemplates>();
        _templateMaterial = new Material(shader);
        _templates.opaque = _templateMaterial;
        _templates.cutout = _templateMaterial;
        _templates.transparent = _templateMaterial;

        var pipeline = new PmxImporterPipeline(
            new PmxParser(),
            reader,
            new PmxMeshBuilder(),
            new PmxBoneBuilder(),
            new PmxMaterialBuilder(_templates, reader),
            new PmxPhysicsBuilder());

        _model = await pipeline.ImportAsync("memory/model.pmx");

        Assert.That(_model, Is.Not.Null);
        Assert.That(_model.name, Is.EqualTo("テストモデル"));
        Assert.That(pipeline.LastImportedDocument.Vertices.Length, Is.EqualTo(3));
        Assert.That(pipeline.LastImportedDocument.Faces.Length, Is.EqualTo(1));

        SkinnedMeshRenderer renderer = _model.GetComponent<SkinnedMeshRenderer>();
        Assert.That(renderer, Is.Not.Null);
        Assert.That(renderer.sharedMesh.vertexCount, Is.EqualTo(3));
        Assert.That(renderer.sharedMesh.subMeshCount, Is.EqualTo(1));
        Assert.That(renderer.sharedMesh.GetTriangles(0), Is.EqualTo(new[] { 0, 1, 2 }));
        Assert.That(renderer.sharedMesh.vertices[0], Is.EqualTo(new Vector3(0, 1, 2)));
        Assert.That(renderer.sharedMesh.uv[0].y, Is.EqualTo(0.25f).Within(1e-4f));
        Assert.That(renderer.sharedMaterials.Length, Is.EqualTo(1));
        Assert.That(renderer.bones.Length, Is.EqualTo(3));
        Assert.That(renderer.bones[1].name, Is.EqualTo("Child"));
        Assert.That(renderer.bones[1].parent.name, Is.EqualTo("Root"));
        Assert.That(_model.transform.localScale.x, Is.EqualTo(0.1f).Within(1e-4f));

        foreach (BoneWeight weight in renderer.sharedMesh.boneWeights)
        {
            Assert.That(weight.weight0 + weight.weight1 + weight.weight2 + weight.weight3, Is.EqualTo(1.0f).Within(1e-4f));
        }
    }

    private sealed class InMemoryReader : IStreamingAssetsReader
    {
        private readonly byte[] _bytes;

        public InMemoryReader(byte[] bytes)
        {
            _bytes = bytes;
        }

        public Task<byte[]> ReadAllBytesAsync(string path)
        {
            return Task.FromResult(_bytes);
        }

        public Task<bool> ExistsAsync(string path)
        {
            return Task.FromResult(true);
        }
    }
}
