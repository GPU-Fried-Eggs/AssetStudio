using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using AssetStudio.FBXWrapper;

namespace AssetStudio.Utility;

public class ModelConverter : IImported
{
    public ImportedFrame RootFrame { get; protected set; }
    public List<ImportedMesh> MeshList { get; protected set; } = new();
    public List<ImportedMaterial> MaterialList { get; protected set; } = new();
    public List<ImportedTexture> TextureList { get; protected set; } = new();
    public List<ImportedKeyframedAnimation> AnimationList { get; protected set; } = new();
    public List<ImportedMorph> MorphList { get; protected set; } = new();

    private ImageFormat m_ImageFormat;
    private Avatar m_Avatar;
    private HashSet<AnimationClip> m_AnimationClipHashSet = new();
    private Dictionary<AnimationClip, string> m_BoundAnimationPathDic = new();
    private Dictionary<uint, string> m_BonePathHash = new();
    private Dictionary<Texture2D, string> m_TextureNameDictionary = new();
    private Dictionary<Transform, ImportedFrame> m_TransformDictionary = new();
    Dictionary<uint, string> m_MorphChannelNames = new();

    public ModelConverter(GameObject gameObject, ImageFormat imageFormat, AnimationClip[] animationList = null)
    {
        this.m_ImageFormat = imageFormat;
        if (gameObject.m_Animator != null)
        {
            InitWithAnimator(gameObject.m_Animator);
            if (animationList == null)
            {
                CollectAnimationClip(gameObject.m_Animator);
            }
        }
        else
        {
            InitWithGameObject(gameObject);
        }
        if (animationList != null)
        {
            foreach (var animationClip in animationList)
            {
                m_AnimationClipHashSet.Add(animationClip);
            }
        }
        ConvertAnimations();
    }

    public ModelConverter(string rootName, List<GameObject> gameObjects, ImageFormat imageFormat, AnimationClip[] animationList = null)
    {
        this.m_ImageFormat = imageFormat;
        RootFrame = CreateFrame(rootName, Vector3.Zero, new Quaternion(0, 0, 0, 0), Vector3.One);
        foreach (var gameObject in gameObjects)
        {
            if (gameObject.m_Animator != null && animationList == null)
            {
                CollectAnimationClip(gameObject.m_Animator);
            }

            var transform = gameObject.m_Transform;
            ConvertTransforms(transform, RootFrame);
            CreateBonePathHash(transform);
        }
        foreach (var gameObject in gameObjects)
        {
            var transform = gameObject.m_Transform;
            ConvertMeshRenderer(transform);
        }
        if (animationList != null)
        {
            foreach (var animationClip in animationList)
            {
                m_AnimationClipHashSet.Add(animationClip);
            }
        }
        ConvertAnimations();
    }

    public ModelConverter(Animator animator, ImageFormat imageFormat, AnimationClip[] animationList = null)
    {
        this.m_ImageFormat = imageFormat;
        InitWithAnimator(animator);
        if (animationList == null)
        {
            CollectAnimationClip(animator);
        }
        else
        {
            foreach (var animationClip in animationList)
            {
                m_AnimationClipHashSet.Add(animationClip);
            }
        }
        ConvertAnimations();
    }

    private void InitWithAnimator(Animator animator)
    {
        if (animator.m_Avatar.TryGet(out var avatar))
            this.m_Avatar = avatar;

        animator.m_GameObject.TryGet(out var gameObject);
        InitWithGameObject(gameObject, animator.m_HasTransformHierarchy);
    }

    private void InitWithGameObject(GameObject gameObject, bool hasTransformHierarchy = true)
    {
        var transform = gameObject.m_Transform;
        if (!hasTransformHierarchy)
        {
            ConvertTransforms(transform, null);
            DeoptimizeTransformHierarchy();
        }
        else
        {
            var frameList = new List<ImportedFrame>();
            var tempTransform = transform;
            while (tempTransform.m_Father.TryGet(out var father))
            {
                frameList.Add(ConvertTransform(father));
                tempTransform = father;
            }
            if (frameList.Count > 0)
            {
                RootFrame = frameList[^1];
                for (var i = frameList.Count - 2; i >= 0; i--)
                {
                    var frame = frameList[i];
                    var parent = frameList[i + 1];
                    parent.AddChild(frame);
                }
                ConvertTransforms(transform, frameList[0]);
            }
            else
            {
                ConvertTransforms(transform, null);
            }

            CreateBonePathHash(transform);
        }

        ConvertMeshRenderer(transform);
    }

    private void ConvertMeshRenderer(Transform transform)
    {
        transform.m_GameObject.TryGet(out var gameObject);

        if (gameObject.m_MeshRenderer != null)
        {
            ConvertMeshRenderer(gameObject.m_MeshRenderer);
        }

        if (gameObject.m_SkinnedMeshRenderer != null)
        {
            ConvertMeshRenderer(gameObject.m_SkinnedMeshRenderer);
        }

        if (gameObject.m_Animation != null)
        {
            foreach (var animation in gameObject.m_Animation.m_Animations)
            {
                if (animation.TryGet(out var animationClip))
                {
                    if (!m_BoundAnimationPathDic.ContainsKey(animationClip))
                    {
                        m_BoundAnimationPathDic.Add(animationClip, GetTransformPath(transform));
                    }
                    m_AnimationClipHashSet.Add(animationClip);
                }
            }
        }

        foreach (var pptr in transform.m_Children)
        {
            if (pptr.TryGet(out var child))
                ConvertMeshRenderer(child);
        }
    }

    private void CollectAnimationClip(Animator animator)
    {
        if (animator.m_Controller.TryGet(out var controller))
        {
            switch (controller)
            {
                case AnimatorOverrideController animatorOverrideController:
                {
                    if (animatorOverrideController.m_Controller.TryGet<AnimatorController>(out var animatorController))
                    {
                        foreach (var pptr in animatorController.m_AnimationClips)
                        {
                            if (pptr.TryGet(out var animationClip))
                            {
                                m_AnimationClipHashSet.Add(animationClip);
                            }
                        }
                    }
                    break;
                }

                case AnimatorController animatorController:
                {
                    foreach (var pptr in animatorController.m_AnimationClips)
                    {
                        if (pptr.TryGet(out var animationClip))
                        {
                            m_AnimationClipHashSet.Add(animationClip);
                        }
                    }
                    break;
                }
            }
        }
    }

    private ImportedFrame ConvertTransform(Transform trans)
    {
        var frame = new ImportedFrame(trans.m_Children.Length);
        m_TransformDictionary.Add(trans, frame);
        trans.m_GameObject.TryGet(out var gameObject);
        frame.Name = gameObject.m_Name;
        SetFrame(frame, trans.m_LocalPosition, trans.m_LocalRotation, trans.m_LocalScale);
        return frame;
    }

    private static ImportedFrame CreateFrame(string name, Vector3 t, Quaternion q, Vector3 s)
    {
        var frame = new ImportedFrame { Name = name };
        SetFrame(frame, t, q, s);
        return frame;
    }

    private static void SetFrame(ImportedFrame frame, Vector3 t, Quaternion q, Vector3 s)
    {
        frame.LocalPosition = new Vector3(-t.X, t.Y, t.Z);
        frame.LocalRotation = Fbx.QuaternionToEuler(new Quaternion(q.X, -q.Y, -q.Z, q.W));
        frame.LocalScale = s;
    }

    private void ConvertTransforms(Transform trans, ImportedFrame parent)
    {
        var frame = ConvertTransform(trans);
        if (parent == null)
        {
            RootFrame = frame;
        }
        else
        {
            parent.AddChild(frame);
        }
        foreach (var pptr in trans.m_Children)
        {
            if (pptr.TryGet(out var child))
                ConvertTransforms(child, frame);
        }
    }

    private void ConvertMeshRenderer(Renderer meshR)
    {
        var mesh = GetMesh(meshR);
        if (mesh == null)
            return;
        var iMesh = new ImportedMesh();
        meshR.m_GameObject.TryGet(out var gameObject2);
        iMesh.Path = GetTransformPath(gameObject2.m_Transform);
        iMesh.SubmeshList = new List<ImportedSubmesh>();
        var subHashSet = new HashSet<int>();
        var combine = false;
        int firstSubMesh = 0;
        if (meshR.m_StaticBatchInfo?.subMeshCount > 0)
        {
            firstSubMesh = meshR.m_StaticBatchInfo.firstSubMesh;
            var finalSubMesh = meshR.m_StaticBatchInfo.firstSubMesh + meshR.m_StaticBatchInfo.subMeshCount;
            for (int i = meshR.m_StaticBatchInfo.firstSubMesh; i < finalSubMesh; i++)
            {
                subHashSet.Add(i);
            }
            combine = true;
        }
        else if (meshR.m_SubsetIndices?.Length > 0)
        {
            firstSubMesh = (int)meshR.m_SubsetIndices.Min(x => x);
            foreach (var index in meshR.m_SubsetIndices)
            {
                subHashSet.Add((int)index);
            }
            combine = true;
        }

        iMesh.hasNormal = mesh.m_Normals?.Length > 0;
        iMesh.hasUV = new bool[8];
        for (int uv = 0; uv < 8; uv++)
        {
            iMesh.hasUV[uv] = mesh.GetUV(uv)?.Length > 0;
        }
        iMesh.hasTangent = mesh.m_Tangents != null && mesh.m_Tangents.Length == mesh.m_VertexCount * 4;
        iMesh.hasColor = mesh.m_Colors?.Length > 0;

        int firstFace = 0;
        for (int i = 0; i < mesh.m_SubMeshes.Length; i++)
        {
            int numFaces = (int)mesh.m_SubMeshes[i].indexCount / 3;
            if (subHashSet.Count > 0 && !subHashSet.Contains(i))
            {
                firstFace += numFaces;
                continue;
            }
            var subMesh = mesh.m_SubMeshes[i];
            var importedSubMesh = new ImportedSubmesh();
            Material mat = null;
            if (i - firstSubMesh < meshR.m_Materials.Length)
            {
                if (meshR.m_Materials[i - firstSubMesh].TryGet(out var material))
                {
                    mat = material;
                }
            }
            var iMat = ConvertMaterial(mat);
            importedSubMesh.Material = iMat.Name;
            importedSubMesh.BaseVertex = (int)mesh.m_SubMeshes[i].firstVertex;

            //Face
            importedSubMesh.FaceList = new List<ImportedFace>(numFaces);
            var end = firstFace + numFaces;
            for (int f = firstFace; f < end; f++)
            {
                var face = new ImportedFace { VertexIndices = new int[3] };
                face.VertexIndices[0] = (int)(mesh.m_Indices[f * 3 + 2] - subMesh.firstVertex);
                face.VertexIndices[1] = (int)(mesh.m_Indices[f * 3 + 1] - subMesh.firstVertex);
                face.VertexIndices[2] = (int)(mesh.m_Indices[f * 3] - subMesh.firstVertex);
                importedSubMesh.FaceList.Add(face);
            }
            firstFace = end;

            iMesh.SubmeshList.Add(importedSubMesh);
        }

        // Shared vertex list
        iMesh.VertexList = new List<ImportedVertex>((int)mesh.m_VertexCount);
        for (var j = 0; j < mesh.m_VertexCount; j++)
        {
            var iVertex = new ImportedVertex();
            //Vertices
            int c = 3;
            if (mesh.m_Vertices.Length == mesh.m_VertexCount * 4)
            {
                c = 4;
            }
            iVertex.Vertex = new Vector3(-mesh.m_Vertices[j * c], mesh.m_Vertices[j * c + 1], mesh.m_Vertices[j * c + 2]);
            //Normals
            if (iMesh.hasNormal)
            {
                if (mesh.m_Normals.Length == mesh.m_VertexCount * 3)
                {
                    c = 3;
                }
                else if (mesh.m_Normals.Length == mesh.m_VertexCount * 4)
                {
                    c = 4;
                }
                iVertex.Normal = new Vector3(-mesh.m_Normals[j * c], mesh.m_Normals[j * c + 1], mesh.m_Normals[j * c + 2]);
            }
            //UV
            iVertex.UV = new float[8][];
            for (int i = 0; i < 8; i++)
            {
                if (iMesh.hasUV[i])
                {
                    var uv = mesh.GetUV(i);
                    if (uv.Length == mesh.m_VertexCount * 2)
                    {
                        c = 2;
                    }
                    else if (uv.Length == mesh.m_VertexCount * 3)
                    {
                        c = 3;
                    }
                    iVertex.UV[i] = new[] { uv[j * c], uv[j * c + 1] };
                }
            }
            //Tangent
            if (iMesh.hasTangent)
            {
                iVertex.Tangent = new Vector4(-mesh.m_Tangents[j * 4], mesh.m_Tangents[j * 4 + 1], mesh.m_Tangents[j * 4 + 2], mesh.m_Tangents[j * 4 + 3]);
            }
            //Colors
            if (iMesh.hasColor)
            {
                if (mesh.m_Colors.Length == mesh.m_VertexCount * 3)
                {
                    iVertex.Color = new Color(mesh.m_Colors[j * 3], mesh.m_Colors[j * 3 + 1], mesh.m_Colors[j * 3 + 2], 1.0f);
                }
                else
                {
                    iVertex.Color = new Color(mesh.m_Colors[j * 4], mesh.m_Colors[j * 4 + 1], mesh.m_Colors[j * 4 + 2], mesh.m_Colors[j * 4 + 3]);
                }
            }
            //BoneInfluence
            if (mesh.m_Skin?.Length > 0)
            {
                var inf = mesh.m_Skin[j];
                iVertex.BoneIndices = new int[4];
                iVertex.Weights = new float[4];
                for (var k = 0; k < 4; k++)
                {
                    iVertex.BoneIndices[k] = inf.boneIndex[k];
                    iVertex.Weights[k] = inf.weight[k];
                }
            }
            iMesh.VertexList.Add(iVertex);
        }

        if (meshR is SkinnedMeshRenderer sMesh)
        {
            //Bone
            /*
             * 0 - None
             * 1 - m_Bones
             * 2 - m_BoneNameHashes
             */
            var boneType = 0;
            if (sMesh.m_Bones.Length > 0)
            {
                if (sMesh.m_Bones.Length == mesh.m_BindPose.Length)
                {
                    var verifiedBoneCount = sMesh.m_Bones.Count(x => x.TryGet(out _));
                    if (verifiedBoneCount > 0)
                    {
                        boneType = 1;
                    }
                    if (verifiedBoneCount != sMesh.m_Bones.Length)
                    {
                        //尝试使用m_BoneNameHashes 4.3 and up
                        if (mesh.m_BindPose.Length > 0 && (mesh.m_BindPose.Length == mesh.m_BoneNameHashes?.Length))
                        {
                            //有效bone数量是否大于SkinnedMeshRenderer
                            var verifiedBoneCount2 = mesh.m_BoneNameHashes.Count(x => FixBonePath(GetPathFromHash(x)) != null);
                            if (verifiedBoneCount2 > verifiedBoneCount)
                            {
                                boneType = 2;
                            }
                        }
                    }
                }
            }
            if (boneType == 0)
            {
                //尝试使用m_BoneNameHashes 4.3 and up
                if (mesh.m_BindPose.Length > 0 && (mesh.m_BindPose.Length == mesh.m_BoneNameHashes?.Length))
                {
                    var verifiedBoneCount = mesh.m_BoneNameHashes.Count(x => FixBonePath(GetPathFromHash(x)) != null);
                    if (verifiedBoneCount > 0)
                    {
                        boneType = 2;
                    }
                }
            }

            if (boneType == 1)
            {
                var boneCount = sMesh.m_Bones.Length;
                iMesh.BoneList = new List<ImportedBone>(boneCount);
                for (int i = 0; i < boneCount; i++)
                {
                    var bone = new ImportedBone();
                    if (sMesh.m_Bones[i].TryGet(out var transform))
                    {
                        bone.Path = GetTransformPath(transform);
                    }
                    var convert = Matrix4x4.Scale(new Vector3(-1, 1, 1));
                    bone.Matrix = convert * mesh.m_BindPose[i] * convert;
                    iMesh.BoneList.Add(bone);
                }
            }
            else if (boneType == 2)
            {
                var boneCount = mesh.m_BindPose.Length;
                iMesh.BoneList = new List<ImportedBone>(boneCount);
                for (int i = 0; i < boneCount; i++)
                {
                    var bone = new ImportedBone();
                    var boneHash = mesh.m_BoneNameHashes[i];
                    var path = GetPathFromHash(boneHash);
                    bone.Path = FixBonePath(path);
                    var convert = Matrix4x4.Scale(new Vector3(-1, 1, 1));
                    bone.Matrix = convert * mesh.m_BindPose[i] * convert;
                    iMesh.BoneList.Add(bone);
                }
            }

            //Morphs
            if (mesh.m_Shapes?.channels?.Length > 0)
            {
                var morph = new ImportedMorph();
                MorphList.Add(morph);
                morph.Path = iMesh.Path;
                morph.Channels = new List<ImportedMorphChannel>(mesh.m_Shapes.channels.Length);
                for (int i = 0; i < mesh.m_Shapes.channels.Length; i++)
                {
                    var channel = new ImportedMorphChannel();
                    morph.Channels.Add(channel);
                    var shapeChannel = mesh.m_Shapes.channels[i];

                    var blendShapeName = "blendShape." + shapeChannel.name;
                    var crc = new SevenZip.CRC();
                    var bytes = Encoding.UTF8.GetBytes(blendShapeName);
                    crc.Update(bytes, 0, (uint)bytes.Length);
                    m_MorphChannelNames[crc.GetDigest()] = blendShapeName;

                    channel.Name = shapeChannel.name.Split('.').Last();
                    channel.KeyframeList = new List<ImportedMorphKeyframe>(shapeChannel.frameCount);
                    var frameEnd = shapeChannel.frameIndex + shapeChannel.frameCount;
                    for (int frameIdx = shapeChannel.frameIndex; frameIdx < frameEnd; frameIdx++)
                    {
                        var keyframe = new ImportedMorphKeyframe();
                        channel.KeyframeList.Add(keyframe);
                        keyframe.Weight = mesh.m_Shapes.fullWeights[frameIdx];
                        var shape = mesh.m_Shapes.shapes[frameIdx];
                        keyframe.hasNormals = shape.hasNormals;
                        keyframe.hasTangents = shape.hasTangents;
                        keyframe.VertexList = new List<ImportedMorphVertex>((int)shape.vertexCount);
                        var vertexEnd = shape.firstVertex + shape.vertexCount;
                        for (uint j = shape.firstVertex; j < vertexEnd; j++)
                        {
                            var destVertex = new ImportedMorphVertex();
                            keyframe.VertexList.Add(destVertex);
                            var morphVertex = mesh.m_Shapes.vertices[j];
                            destVertex.Index = morphVertex.index;
                            var sourceVertex = iMesh.VertexList[(int)morphVertex.index];
                            destVertex.Vertex = new ImportedVertex();
                            var morphPos = morphVertex.vertex;
                            destVertex.Vertex.Vertex = sourceVertex.Vertex + new Vector3(-morphPos.X, morphPos.Y, morphPos.Z);
                            if (shape.hasNormals)
                            {
                                var morphNormal = morphVertex.normal;
                                destVertex.Vertex.Normal = new Vector3(-morphNormal.X, morphNormal.Y, morphNormal.Z);
                            }
                            if (shape.hasTangents)
                            {
                                var morphTangent = morphVertex.tangent;
                                destVertex.Vertex.Tangent = new Vector4(-morphTangent.X, morphTangent.Y, morphTangent.Z, 0);
                            }
                        }
                    }
                }
            }
        }

        //TODO combine mesh
        if (combine)
        {
            meshR.m_GameObject.TryGet(out var gameObject);
            var frame = RootFrame.FindChild(gameObject.m_Name);
            if (frame != null)
            {
                frame.LocalPosition = RootFrame.LocalPosition;
                frame.LocalRotation = RootFrame.LocalRotation;
                while (frame.Parent != null)
                {
                    frame = frame.Parent;
                    frame.LocalPosition = RootFrame.LocalPosition;
                    frame.LocalRotation = RootFrame.LocalRotation;
                }
            }
        }

        MeshList.Add(iMesh);
    }

    private static Mesh GetMesh(Renderer meshR)
    {
        if (meshR is SkinnedMeshRenderer sMesh)
        {
            if (sMesh.m_Mesh.TryGet(out var mesh)) return mesh;
        }
        else
        {
            meshR.m_GameObject.TryGet(out var gameObject);
            if (gameObject.m_MeshFilter != null)
            {
                if (gameObject.m_MeshFilter.m_Mesh.TryGet(out var mesh))
                {
                    return mesh;
                }
            }
        }

        return null;
    }

    private string GetTransformPath(Transform transform)
    {
        if (m_TransformDictionary.TryGetValue(transform, out var frame))
        {
            return frame.Path;
        }
        return null;
    }

    private string FixBonePath(AnimationClip animationClip, string path)
    {
        if (m_BoundAnimationPathDic.TryGetValue(animationClip, out var basePath))
        {
            path = basePath + "/" + path;
        }
        return FixBonePath(path);
    }

    private string FixBonePath(string path)
    {
        var frame = RootFrame.FindFrameByPath(path);
        return frame?.Path;
    }

    private static string GetTransformPathByFather(Transform transform)
    {
        transform.m_GameObject.TryGet(out var gameObject);
        if (transform.m_Father.TryGet(out var father))
        {
            return GetTransformPathByFather(father) + "/" + gameObject.m_Name;
        }

        return gameObject.m_Name;
    }

    private ImportedMaterial ConvertMaterial(Material mat)
    {
        ImportedMaterial iMat;
        if (mat != null)
        {
            iMat = ImportedHelpers.FindMaterial(mat.m_Name, MaterialList);
            if (iMat != null) return iMat;
            iMat = new ImportedMaterial
            {
                Name = mat.m_Name,
                //default values
                Diffuse = new Color(0.8f, 0.8f, 0.8f, 1),
                Ambient = new Color(0.2f, 0.2f, 0.2f, 1),
                Emissive = new Color(0, 0, 0, 1),
                Specular = new Color(0.2f, 0.2f, 0.2f, 1),
                Reflection = new Color(0, 0, 0, 1),
                Shininess = 20f,
                Transparency = 0f
            };
            foreach (var col in mat.m_SavedProperties.m_Colors)
            {
                switch (col.Key)
                {
                    case "_Color":
                        iMat.Diffuse = col.Value;
                        break;
                    case "_SColor":
                        iMat.Ambient = col.Value;
                        break;
                    case "_EmissionColor":
                        iMat.Emissive = col.Value;
                        break;
                    case "_SpecularColor":
                        iMat.Specular = col.Value;
                        break;
                    case "_ReflectColor":
                        iMat.Reflection = col.Value;
                        break;
                }
            }

            foreach (var flt in mat.m_SavedProperties.m_Floats)
            {
                switch (flt.Key)
                {
                    case "_Shininess":
                        iMat.Shininess = flt.Value;
                        break;
                    case "_Transparency":
                        iMat.Transparency = flt.Value;
                        break;
                }
            }

            //textures
            iMat.Textures = new List<ImportedMaterialTexture>();
            foreach (var texEnv in mat.m_SavedProperties.m_TexEnvs)
            {
                if (!texEnv.Value.m_Texture.TryGet<Texture2D>(out var texture2D)) //TODO other Texture
                {
                    continue;
                }

                var texture = new ImportedMaterialTexture();
                iMat.Textures.Add(texture);

                int dest = -1;
                if (texEnv.Key == "_MainTex")
                    dest = 0;
                else if (texEnv.Key == "_BumpMap")
                    dest = 3;
                else if (texEnv.Key.Contains("Specular"))
                    dest = 2;
                else if (texEnv.Key.Contains("Normal"))
                    dest = 1;

                texture.Dest = dest;

                var ext = $".{m_ImageFormat.ToString().ToLower()}";
                if (m_TextureNameDictionary.TryGetValue(texture2D, out var textureName))
                {
                    texture.Name = textureName;
                }
                else if (ImportedHelpers.FindTexture(texture2D.m_Name + ext, TextureList) != null) //已有相同名字的图片
                {
                    for (int i = 1; ; i++)
                    {
                        var name = texture2D.m_Name + $" ({i}){ext}";
                        if (ImportedHelpers.FindTexture(name, TextureList) == null)
                        {
                            texture.Name = name;
                            m_TextureNameDictionary.Add(texture2D, name);
                            break;
                        }
                    }
                }
                else
                {
                    texture.Name = texture2D.m_Name + ext;
                    m_TextureNameDictionary.Add(texture2D, texture.Name);
                }

                texture.Offset = texEnv.Value.m_Offset;
                texture.Scale = texEnv.Value.m_Scale;
                ConvertTexture2D(texture2D, texture.Name);
            }

            MaterialList.Add(iMat);
        }
        else
        {
            iMat = new ImportedMaterial();
        }
        return iMat;
    }

    private void ConvertTexture2D(Texture2D texture2D, string name)
    {
        var iTex = ImportedHelpers.FindTexture(name, TextureList);
        if (iTex != null) return;

        var stream = texture2D.ConvertToStream(m_ImageFormat, true);
        if (stream != null)
        {
            using (stream)
            {
                iTex = new ImportedTexture(stream, name);
                TextureList.Add(iTex);
            }
        }
    }

    private void ConvertAnimations()
    {
        foreach (var animationClip in m_AnimationClipHashSet)
        {
            var iAnim = new ImportedKeyframedAnimation();
            var name = animationClip.m_Name;
            if (AnimationList.Exists(x => x.Name == name))
            {
                for (int i = 1; ; i++)
                {
                    var fixName = name + $"_{i}";
                    if (!AnimationList.Exists(x => x.Name == fixName))
                    {
                        name = fixName;
                        break;
                    }
                }
            }
            iAnim.Name = name;
            iAnim.SampleRate = animationClip.m_SampleRate;
            iAnim.TrackList = new List<ImportedAnimationKeyframedTrack>();
            AnimationList.Add(iAnim);
            if (animationClip.m_Legacy)
            {
                foreach (var compressedRotationCurve in animationClip.m_CompressedRotationCurves)
                {
                    var track = iAnim.FindTrack(FixBonePath(animationClip, compressedRotationCurve.m_Path));

                    var numKeys = compressedRotationCurve.m_Times.m_NumItems;
                    var data = compressedRotationCurve.m_Times.UnpackInts();
                    var times = new float[numKeys];
                    int t = 0;
                    for (int i = 0; i < numKeys; i++)
                    {
                        t += data[i];
                        times[i] = t * 0.01f;
                    }
                    var quats = compressedRotationCurve.m_Values.UnpackQuats();

                    for (int i = 0; i < numKeys; i++)
                    {
                        var quat = quats[i];
                        var value = Fbx.QuaternionToEuler(new Quaternion(quat.X, -quat.Y, -quat.Z, quat.W));
                        track.Rotations.Add(new ImportedKeyframe<Vector3>(times[i], value));
                    }
                }
                foreach (var rotationCurve in animationClip.m_RotationCurves)
                {
                    var track = iAnim.FindTrack(FixBonePath(animationClip, rotationCurve.path));
                    foreach (var curve in rotationCurve.curve.m_Curve)
                    {
                        var value = Fbx.QuaternionToEuler(new Quaternion(curve.value.X, -curve.value.Y, -curve.value.Z, curve.value.W));
                        track.Rotations.Add(new ImportedKeyframe<Vector3>(curve.time, value));
                    }
                }
                foreach (var positionCurve in animationClip.m_PositionCurves)
                {
                    var track = iAnim.FindTrack(FixBonePath(animationClip, positionCurve.path));
                    foreach (var curve in positionCurve.curve.m_Curve)
                    {
                        track.Translations.Add(new ImportedKeyframe<Vector3>(curve.time, new Vector3(-curve.value.X, curve.value.Y, curve.value.Z)));
                    }
                }
                foreach (var scaleCurve in animationClip.m_ScaleCurves)
                {
                    var track = iAnim.FindTrack(FixBonePath(animationClip, scaleCurve.path));
                    foreach (var curve in scaleCurve.curve.m_Curve)
                    {
                        track.Scalings.Add(new ImportedKeyframe<Vector3>(curve.time, new Vector3(curve.value.X, curve.value.Y, curve.value.Z)));
                    }
                }
                if (animationClip.m_EulerCurves != null)
                {
                    foreach (var eulerCurve in animationClip.m_EulerCurves)
                    {
                        var track = iAnim.FindTrack(FixBonePath(animationClip, eulerCurve.path));
                        foreach (var curve in eulerCurve.curve.m_Curve)
                        {
                            track.Rotations.Add(new ImportedKeyframe<Vector3>(curve.time, new Vector3(curve.value.X, -curve.value.Y, -curve.value.Z)));
                        }
                    }
                }
                foreach (var floatCurve in animationClip.m_FloatCurves)
                {
                    if (floatCurve.classID == ClassIDType.SkinnedMeshRenderer) //BlendShape
                    {
                        var channelName = floatCurve.attribute;
                        int dotPos = channelName.IndexOf('.');
                        if (dotPos >= 0)
                        {
                            channelName = channelName.Substring(dotPos + 1);
                        }

                        var path = FixBonePath(animationClip, floatCurve.path);
                        if (string.IsNullOrEmpty(path))
                        {
                            path = GetPathByChannelName(channelName);
                        }
                        var track = iAnim.FindTrack(path);
                        track.BlendShape = new ImportedBlendShape();
                        track.BlendShape.ChannelName = channelName;
                        foreach (var curve in floatCurve.curve.m_Curve)
                        {
                            track.BlendShape.Keyframes.Add(new ImportedKeyframe<float>(curve.time, curve.value));
                        }
                    }
                }
            }
            else
            {
                var clip = animationClip.m_MuscleClip.m_Clip;
                var streamedFrames = clip.m_StreamedClip.ReadData();
                var clipBindingConstant = animationClip.m_ClipBindingConstant ?? clip.ConvertValueArrayToGenericBinding();
                for (int frameIndex = 1; frameIndex < streamedFrames.Count - 1; frameIndex++)
                {
                    var frame = streamedFrames[frameIndex];
                    var streamedValues = frame.keyList.Select(x => x.value).ToArray();
                    for (int curveIndex = 0; curveIndex < frame.keyList.Length;)
                    {
                        ReadCurveData(iAnim, clipBindingConstant, frame.keyList[curveIndex].index, frame.time, streamedValues, 0, ref curveIndex);
                    }
                }
                var denseClip = clip.m_DenseClip;
                var streamCount = clip.m_StreamedClip.curveCount;
                for (int frameIndex = 0; frameIndex < denseClip.m_FrameCount; frameIndex++)
                {
                    var time = denseClip.m_BeginTime + frameIndex / denseClip.m_SampleRate;
                    var frameOffset = frameIndex * denseClip.m_CurveCount;
                    for (int curveIndex = 0; curveIndex < denseClip.m_CurveCount;)
                    {
                        var index = streamCount + curveIndex;
                        ReadCurveData(iAnim, clipBindingConstant, (int)index, time, denseClip.m_SampleArray, (int)frameOffset, ref curveIndex);
                    }
                }
                if (clip.m_ConstantClip != null)
                {
                    var constantClip = clip.m_ConstantClip;
                    var denseCount = clip.m_DenseClip.m_CurveCount;
                    var time2 = 0.0f;
                    for (int i = 0; i < 2; i++)
                    {
                        for (int curveIndex = 0; curveIndex < constantClip.data.Length;)
                        {
                            var index = streamCount + denseCount + curveIndex;
                            ReadCurveData(iAnim, clipBindingConstant, (int)index, time2, constantClip.data, 0, ref curveIndex);
                        }
                        time2 = animationClip.m_MuscleClip.m_StopTime;
                    }
                }
            }
        }
    }

    private void ReadCurveData(ImportedKeyframedAnimation iAnim, AnimationClipBindingConstant clipBindingConstant, int index, float time, float[] data, int offset, ref int curveIndex)
    {
        var binding = clipBindingConstant.FindBinding(index);
        if (binding.typeID == ClassIDType.SkinnedMeshRenderer) //BlendShape
        {
            var channelName = GetChannelNameFromHash(binding.attribute);
            if (string.IsNullOrEmpty(channelName))
            {
                curveIndex++;
                return;
            }
            int dotPos = channelName.IndexOf('.');
            if (dotPos >= 0)
            {
                channelName = channelName.Substring(dotPos + 1);
            }

            var bPath = FixBonePath(GetPathFromHash(binding.path));
            if (string.IsNullOrEmpty(bPath))
            {
                bPath = GetPathByChannelName(channelName);
            }
            var bTrack = iAnim.FindTrack(bPath);
            bTrack.BlendShape = new ImportedBlendShape { ChannelName = channelName };
            bTrack.BlendShape.Keyframes.Add(new ImportedKeyframe<float>(time, data[curveIndex++ + offset]));
        }
        else if (binding.typeID == ClassIDType.Transform)
        {
            var path = FixBonePath(GetPathFromHash(binding.path));
            var track = iAnim.FindTrack(path);

            switch (binding.attribute)
            {
                case 1:
                    track.Translations.Add(new ImportedKeyframe<Vector3>(time, new Vector3
                    (
                        -data[curveIndex++ + offset],
                        data[curveIndex++ + offset],
                        data[curveIndex++ + offset]
                    )));
                    break;
                case 2:
                    var value = Fbx.QuaternionToEuler(new Quaternion
                    (
                        data[curveIndex++ + offset],
                        -data[curveIndex++ + offset],
                        -data[curveIndex++ + offset],
                        data[curveIndex++ + offset]
                    ));
                    track.Rotations.Add(new ImportedKeyframe<Vector3>(time, value));
                    break;
                case 3:
                    track.Scalings.Add(new ImportedKeyframe<Vector3>(time, new Vector3
                    (
                        data[curveIndex++ + offset],
                        data[curveIndex++ + offset],
                        data[curveIndex++ + offset]
                    )));
                    break;
                case 4:
                    track.Rotations.Add(new ImportedKeyframe<Vector3>(time, new Vector3
                    (
                        data[curveIndex++ + offset],
                        -data[curveIndex++ + offset],
                        -data[curveIndex++ + offset]
                    )));
                    break;
                default:
                    curveIndex++;
                    break;
            }
        }
        else
        {
            curveIndex++;
        }
    }

    private string GetPathFromHash(uint hash)
    {
        m_BonePathHash.TryGetValue(hash, out var boneName);
        if (string.IsNullOrEmpty(boneName))
        {
            boneName = m_Avatar?.FindBonePath(hash);
        }
        if (string.IsNullOrEmpty(boneName))
        {
            boneName = "unknown " + hash;
        }
        return boneName;
    }

    private void CreateBonePathHash(Transform transform)
    {
        var name = GetTransformPathByFather(transform);
        var crc = new SevenZip.CRC();
        var bytes = Encoding.UTF8.GetBytes(name);
        crc.Update(bytes, 0, (uint)bytes.Length);
        m_BonePathHash[crc.GetDigest()] = name;
        int index;
        while ((index = name.IndexOf("/", StringComparison.Ordinal)) >= 0)
        {
            name = name.Substring(index + 1);
            crc = new SevenZip.CRC();
            bytes = Encoding.UTF8.GetBytes(name);
            crc.Update(bytes, 0, (uint)bytes.Length);
            m_BonePathHash[crc.GetDigest()] = name;
        }
        foreach (var pptr in transform.m_Children)
        {
            if (pptr.TryGet(out var child))
                CreateBonePathHash(child);
        }
    }

    private void DeoptimizeTransformHierarchy()
    {
        if (m_Avatar == null)
            throw new Exception("Transform hierarchy has been optimized, but can't find Avatar to deoptimize.");
        // 1. Figure out the skeletonPaths from the unstripped avatar
        var skeletonPaths = new List<string>();
        foreach (var id in m_Avatar.m_Avatar.m_AvatarSkeleton.m_ID)
        {
            var path = m_Avatar.FindBonePath(id);
            skeletonPaths.Add(path);
        }
        // 2. Restore the original transform hierarchy
        // Prerequisite: skeletonPaths follow pre-order traversal
        for (var i = 1; i < skeletonPaths.Count; i++) // start from 1, skip the root transform because it will always be there.
        {
            var path = skeletonPaths[i];
            var strs = path.Split('/');
            string transformName;
            ImportedFrame parentFrame;
            if (strs.Length == 1)
            {
                transformName = path;
                parentFrame = RootFrame;
            }
            else
            {
                transformName = strs.Last();
                var parentFramePath = path.Substring(0, path.LastIndexOf('/'));
                parentFrame = RootFrame.FindRelativeFrameWithPath(parentFramePath);
            }
            var skeletonPose = m_Avatar.m_Avatar.m_DefaultPose;
            var xform = skeletonPose.m_X[i];
            var frame = RootFrame.FindChild(transformName);
            if (frame != null)
            {
                SetFrame(frame, xform.t, xform.q, xform.s);
            }
            else
            {
                frame = CreateFrame(transformName, xform.t, xform.q, xform.s);
            }
            parentFrame.AddChild(frame);
        }
    }

    private string GetPathByChannelName(string channelName)
    {
        foreach (var morph in MorphList)
        {
            foreach (var channel in morph.Channels)
            {
                if (channel.Name == channelName)
                {
                    return morph.Path;
                }
            }
        }
        return null;
    }

    private string GetChannelNameFromHash(uint attribute)
    {
        return m_MorphChannelNames.TryGetValue(attribute, out var name) ? name : null;
    }
}