using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace AssetStudio.FBXWrapper;

internal sealed class FbxExporter : IDisposable
{
    private readonly FbxExporterContext m_Context;

    private readonly string m_FileName;
    private readonly IImported m_Imported;
    private readonly bool m_AllNodes;
    private readonly bool m_ExportSkins;
    private readonly bool m_CastToBone;
    private readonly float m_BoneSize;
    private readonly bool m_ExportAllUvsAsDiffuseMaps;
    private readonly float m_ScaleFactor;
    private readonly int m_VersionIndex;
    private readonly bool m_IsAscii;

    internal FbxExporter(string fileName, IImported imported, bool allNodes, bool exportSkins, bool castToBone, float boneSize, bool exportAllUvsAsDiffuseMaps, float scaleFactor, int versionIndex, bool isAscii)
    {
        m_Context = new FbxExporterContext();

        m_FileName = fileName;
        m_Imported = imported;
        m_AllNodes = allNodes;
        m_ExportSkins = exportSkins;
        m_CastToBone = castToBone;
        m_BoneSize = boneSize;
        m_ExportAllUvsAsDiffuseMaps = exportAllUvsAsDiffuseMaps;
        m_ScaleFactor = scaleFactor;
        m_VersionIndex = versionIndex;
        m_IsAscii = isAscii;
    }

    ~FbxExporter()
    {
        Dispose(false);
    }

    public void Dispose()
    {
        if (IsDisposed) return;

        Dispose(true);
        GC.SuppressFinalize(this);
    }

    public bool IsDisposed { get; private set; }

    private void Dispose(bool disposing)
    {
        if (disposing) m_Context.Dispose();

        IsDisposed = true;
    }

    internal void Initialize()
    {
        var is60Fps = m_Imported.AnimationList.Count > 0 && m_Imported.AnimationList[0].SampleRate.Equals(60.0f);

        m_Context.Initialize(m_FileName, m_ScaleFactor, m_VersionIndex, m_IsAscii, is60Fps);

        if (!m_AllNodes)
        {
            var framePaths = SearchHierarchy();

            m_Context.SetFramePaths(framePaths);
        }
    }

    internal void ExportAll(bool blendShape, bool animation, bool eulerFilter, float filterPrecision)
    {
        var meshFrames = new List<ImportedFrame>();

        ExportRootFrame(meshFrames);

        if (m_Imported.MeshList != null)
        {
            SetJointsFromImportedMeshes();

            PrepareMaterials();

            ExportMeshFrames(m_Imported.RootFrame, meshFrames);
        }
        else
        {
            SetJointsNode(m_Imported.RootFrame, null, true);
        }



        if (blendShape)
        {
            ExportMorphs();
        }

        if (animation)
        {
            ExportAnimations(eulerFilter, filterPrecision);
        }

        ExportScene();
    }

    private void ExportMorphs()
    {
        m_Context.ExportMorphs(m_Imported.RootFrame, m_Imported.MorphList);
    }

    private void ExportAnimations(bool eulerFilter, float filterPrecision)
    {
        m_Context.ExportAnimations(m_Imported.RootFrame, m_Imported.AnimationList, eulerFilter, filterPrecision);
    }

    private void ExportRootFrame(List<ImportedFrame> meshFrames)
    {
        m_Context.ExportFrame(m_Imported.MeshList, meshFrames, m_Imported.RootFrame);
    }

    private void ExportScene()
    {
        m_Context.ExportScene();
    }

    private void SetJointsFromImportedMeshes()
    {
        if (!m_ExportSkins) return;

        Debug.Assert(m_Imported.MeshList != null);

        var bonePaths = new HashSet<string>();

        foreach (var mesh in m_Imported.MeshList)
        {
            var boneList = mesh.BoneList;

            if (boneList != null)
            {
                foreach (var bone in boneList)
                {
                    bonePaths.Add(bone.Path);
                }
            }
        }

        SetJointsNode(m_Imported.RootFrame, bonePaths, m_CastToBone);
    }

    private void SetJointsNode(ImportedFrame rootFrame, HashSet<string>? bonePaths, bool castToBone)
    {
        m_Context.SetJointsNode(rootFrame, bonePaths, castToBone, m_BoneSize);
    }

    private void PrepareMaterials()
    {
        m_Context.PrepareMaterials(m_Imported.MaterialList.Count, m_Imported.TextureList.Count);
    }

    private void ExportMeshFrames(ImportedFrame rootFrame, List<ImportedFrame> meshFrames)
    {
        foreach (var meshFrame in meshFrames)
        {
            m_Context.ExportMeshFromFrame(rootFrame, meshFrame, m_Imported.MeshList, m_Imported.MaterialList, m_Imported.TextureList, m_ExportSkins, m_ExportAllUvsAsDiffuseMaps);
        }
    }

    private HashSet<string>? SearchHierarchy()
    {
        if (m_Imported.MeshList == null || m_Imported.MeshList.Count == 0) return null;

        var exportFrames = new HashSet<string>();

        SearchHierarchy(m_Imported.RootFrame, m_Imported.MeshList, exportFrames);

        return exportFrames;
    }

    private static void SearchHierarchy(ImportedFrame rootFrame, List<ImportedMesh> meshList, HashSet<string> exportFrames)
    {
        var frameStack = new Stack<ImportedFrame>();

        frameStack.Push(rootFrame);

        while (frameStack.Count > 0)
        {
            var frame = frameStack.Pop();

            var meshListSome = ImportedHelpers.FindMesh(frame.Path, meshList);

            if (meshListSome != null)
            {
                var parent = frame;

                while (parent != null)
                {
                    exportFrames.Add(parent.Path);
                    parent = parent.Parent;
                }

                var boneList = meshListSome.BoneList;

                if (boneList != null)
                {
                    foreach (var bone in boneList)
                    {
                        if (!exportFrames.Contains(bone.Path))
                        {
                            var boneParent = rootFrame.FindFrameByPath(bone.Path);

                            while (boneParent != null)
                            {
                                exportFrames.Add(boneParent.Path);
                                boneParent = boneParent.Parent;
                            }
                        }
                    }
                }
            }

            for (var i = frame.Count - 1; i >= 0; i -= 1)
            {
                frameStack.Push(frame[i]);
            }
        }
    }
}