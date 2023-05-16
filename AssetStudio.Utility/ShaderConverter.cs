using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using K4os.Compression.LZ4;

namespace AssetStudio.Utility;

public static class ShaderConverter
{
    public static string Convert(this Shader shader)
    {
        if (shader.m_SubProgramBlob != null) //5.3 - 5.4
        {
            var decompressedBytes = new byte[shader.decompressedSize];
            LZ4Codec.Decode(shader.m_SubProgramBlob, decompressedBytes);
            using var blobReader = new BinaryReader(new MemoryStream(decompressedBytes));
            var program = new ShaderProgram(blobReader, shader.version);
            program.Read(blobReader, 0);
            return header + program.Export(Encoding.UTF8.GetString(shader.m_Script));
        }

        if (shader.compressedBlob != null) //5.5 and up
        {
            return header + ConvertSerializedShader(shader);
        }

        return header + Encoding.UTF8.GetString(shader.m_Script);
    }

    private static string ConvertSerializedShader(Shader shader)
    {
        var length = shader.platforms.Length;
        var shaderPrograms = new ShaderProgram[length];
        for (var i = 0; i < length; i++)
        {
            for (var j = 0; j < shader.offsets[i].Length; j++)
            {
                var offset = shader.offsets[i][j];
                var compressedLength = shader.compressedLengths[i][j];
                var decompressedLength = shader.decompressedLengths[i][j];
                var decompressedBytes = new byte[decompressedLength];
                LZ4Codec.Decode(shader.compressedBlob, (int)offset, (int)compressedLength, decompressedBytes, 0, (int)decompressedLength);
                using var blobReader = new BinaryReader(new MemoryStream(decompressedBytes));
                if (j == 0)
                {
                    shaderPrograms[i] = new ShaderProgram(blobReader, shader.version);
                }
                shaderPrograms[i].Read(blobReader, j);
            }
        }

        return ConvertSerializedShader(shader.m_ParsedForm, shader.platforms, shaderPrograms);
    }

    private static string ConvertSerializedShader(SerializedShader parsedForm, ShaderCompilerPlatform[] platforms, ShaderProgram[] shaderPrograms)
    {
        var sb = new StringBuilder();
        sb.Append($"Shader \"{parsedForm.m_Name}\" {{\n");

        sb.Append(ConvertSerializedProperties(parsedForm.m_PropInfo));

        foreach (var subShader in parsedForm.m_SubShaders)
        {
            sb.Append(ConvertSerializedSubShader(subShader, platforms, shaderPrograms));
        }

        if (!string.IsNullOrEmpty(parsedForm.m_FallbackName))
        {
            sb.Append($"Fallback \"{parsedForm.m_FallbackName}\"\n");
        }

        if (!string.IsNullOrEmpty(parsedForm.m_CustomEditorName))
        {
            sb.Append($"CustomEditor \"{parsedForm.m_CustomEditorName}\"\n");
        }

        sb.Append("}");
        return sb.ToString();
    }

    private static string ConvertSerializedSubShader(SerializedSubShader subShader, ShaderCompilerPlatform[] platforms, ShaderProgram[] shaderPrograms)
    {
        var sb = new StringBuilder();
        sb.Append("SubShader {\n");
        if (subShader.m_LOD != 0)
        {
            sb.Append($" LOD {subShader.m_LOD}\n");
        }

        sb.Append(ConvertSerializedTagMap(subShader.m_Tags, 1));

        foreach (var passe in subShader.m_Passes)
        {
            sb.Append(ConvertSerializedPass(passe, platforms, shaderPrograms));
        }
        sb.Append("}\n");
        return sb.ToString();
    }

    private static string ConvertSerializedPass(SerializedPass passe, ShaderCompilerPlatform[] platforms, ShaderProgram[] shaderPrograms)
    {
        var sb = new StringBuilder();
        switch (passe.m_Type)
        {
            case PassType.Normal:
                sb.Append(" Pass ");
                break;
            case PassType.Use:
                sb.Append(" UsePass ");
                break;
            case PassType.Grab:
                sb.Append(" GrabPass ");
                break;
        }
        if (passe.m_Type == PassType.Use)
        {
            sb.Append($"\"{passe.m_UseName}\"\n");
        }
        else
        {
            sb.Append("{\n");

            if (passe.m_Type == PassType.Grab)
            {
                if (!string.IsNullOrEmpty(passe.m_TextureName))
                {
                    sb.Append($"  \"{passe.m_TextureName}\"\n");
                }
            }
            else
            {
                sb.Append(ConvertSerializedShaderState(passe.m_State));

                if (passe.progVertex.m_SubPrograms.Length > 0)
                {
                    sb.Append("Program \"vp\" {\n");
                    sb.Append(ConvertSerializedSubPrograms(passe.progVertex.m_SubPrograms, platforms, shaderPrograms));
                    sb.Append("}\n");
                }

                if (passe.progFragment.m_SubPrograms.Length > 0)
                {
                    sb.Append("Program \"fp\" {\n");
                    sb.Append(ConvertSerializedSubPrograms(passe.progFragment.m_SubPrograms, platforms, shaderPrograms));
                    sb.Append("}\n");
                }

                if (passe.progGeometry.m_SubPrograms.Length > 0)
                {
                    sb.Append("Program \"gp\" {\n");
                    sb.Append(ConvertSerializedSubPrograms(passe.progGeometry.m_SubPrograms, platforms, shaderPrograms));
                    sb.Append("}\n");
                }

                if (passe.progHull.m_SubPrograms.Length > 0)
                {
                    sb.Append("Program \"hp\" {\n");
                    sb.Append(ConvertSerializedSubPrograms(passe.progHull.m_SubPrograms, platforms, shaderPrograms));
                    sb.Append("}\n");
                }

                if (passe.progDomain.m_SubPrograms.Length > 0)
                {
                    sb.Append("Program \"dp\" {\n");
                    sb.Append(ConvertSerializedSubPrograms(passe.progDomain.m_SubPrograms, platforms, shaderPrograms));
                    sb.Append("}\n");
                }

                if (passe.progRayTracing?.m_SubPrograms.Length > 0)
                {
                    sb.Append("Program \"rtp\" {\n");
                    sb.Append(ConvertSerializedSubPrograms(passe.progRayTracing.m_SubPrograms, platforms, shaderPrograms));
                    sb.Append("}\n");
                }
            }
            sb.Append("}\n");
        }
        return sb.ToString();
    }

    private static string ConvertSerializedSubPrograms(SerializedSubProgram[] serializedSubPrograms, ShaderCompilerPlatform[] platforms, ShaderProgram[] shaderPrograms)
    {
        var sb = new StringBuilder();
        var groups = serializedSubPrograms.GroupBy(x => x.m_BlobIndex);
        foreach (var group in groups)
        {
            var programs = group.GroupBy(x => x.m_GpuProgramType);
            foreach (var program in programs)
            {
                for (int i = 0; i < platforms.Length; i++)
                {
                    var platform = platforms[i];
                    if (CheckGpuProgramUsable(platform, program.Key))
                    {
                        var subPrograms = program.ToList();
                        var isTier = subPrograms.Count > 1;
                        foreach (var subProgram in subPrograms)
                        {
                            sb.Append($"SubProgram \"{GetPlatformString(platform)} ");
                            if (isTier)
                            {
                                sb.Append($"hw_tier{subProgram.m_ShaderHardwareTier:00} ");
                            }
                            sb.Append("\" {\n");
                            sb.Append(shaderPrograms[i].SubPrograms[subProgram.m_BlobIndex].Export());
                            sb.Append("\n}\n");
                        }
                        break;
                    }
                }
            }
        }
        return sb.ToString();
    }

    private static string ConvertSerializedShaderState(SerializedShaderState state)
    {
        var sb = new StringBuilder();
        if (!string.IsNullOrEmpty(state.m_Name))
        {
            sb.Append($"  Name \"{state.m_Name}\"\n");
        }
        if (state.m_LOD != 0)
        {
            sb.Append($"  LOD {state.m_LOD}\n");
        }

        sb.Append(ConvertSerializedTagMap(state.m_Tags, 2));

        sb.Append(ConvertSerializedShaderRTBlendState(state.rtBlend, state.rtSeparateBlend));

        if (state.alphaToMask.val > 0f)
        {
            sb.Append("  AlphaToMask On\n");
        }

        if (state.zClip?.val != 1f) //ZClip On
        {
            sb.Append("  ZClip Off\n");
        }

        if (state.zTest.val != 4f) //ZTest LEqual
        {
            sb.Append("  ZTest ");
            switch (state.zTest.val) //enum CompareFunction
            {
                case 0f: //kFuncDisabled
                    sb.Append("Off");
                    break;
                case 1f: //kFuncNever
                    sb.Append("Never");
                    break;
                case 2f: //kFuncLess
                    sb.Append("Less");
                    break;
                case 3f: //kFuncEqual
                    sb.Append("Equal");
                    break;
                case 5f: //kFuncGreater
                    sb.Append("Greater");
                    break;
                case 6f: //kFuncNotEqual
                    sb.Append("NotEqual");
                    break;
                case 7f: //kFuncGEqual
                    sb.Append("GEqual");
                    break;
                case 8f: //kFuncAlways
                    sb.Append("Always");
                    break;
            }

            sb.Append("\n");
        }

        if (state.zWrite.val != 1f) //ZWrite On
        {
            sb.Append("  ZWrite Off\n");
        }

        if (state.culling.val != 2f) //Cull Back
        {
            sb.Append("  Cull ");
            switch (state.culling.val) //enum CullMode
            {
                case 0f: //kCullOff
                    sb.Append("Off");
                    break;
                case 1f: //kCullFront
                    sb.Append("Front");
                    break;
            }
            sb.Append("\n");
        }

        if (state.offsetFactor.val != 0f || state.offsetUnits.val != 0f)
        {
            sb.Append($"  Offset {state.offsetFactor.val}, {state.offsetUnits.val}\n");
        }

        if (state.stencilRef.val != 0f ||
            state.stencilReadMask.val != 255f ||
            state.stencilWriteMask.val != 255f ||
            state.stencilOp.pass.val != 0f ||
            state.stencilOp.fail.val != 0f ||
            state.stencilOp.zFail.val != 0f ||
            state.stencilOp.comp.val != 8f ||
            state.stencilOpFront.pass.val != 0f ||
            state.stencilOpFront.fail.val != 0f ||
            state.stencilOpFront.zFail.val != 0f ||
            state.stencilOpFront.comp.val != 8f ||
            state.stencilOpBack.pass.val != 0f ||
            state.stencilOpBack.fail.val != 0f ||
            state.stencilOpBack.zFail.val != 0f ||
            state.stencilOpBack.comp.val != 8f)
        {
            sb.Append("  Stencil {\n");
            if (state.stencilRef.val != 0f)
            {
                sb.Append($"   Ref {state.stencilRef.val}\n");
            }
            if (state.stencilReadMask.val != 255f)
            {
                sb.Append($"   ReadMask {state.stencilReadMask.val}\n");
            }
            if (state.stencilWriteMask.val != 255f)
            {
                sb.Append($"   WriteMask {state.stencilWriteMask.val}\n");
            }
            if (state.stencilOp.pass.val != 0f ||
                state.stencilOp.fail.val != 0f ||
                state.stencilOp.zFail.val != 0f ||
                state.stencilOp.comp.val != 8f)
            {
                sb.Append(ConvertSerializedStencilOp(state.stencilOp, ""));
            }
            if (state.stencilOpFront.pass.val != 0f ||
                state.stencilOpFront.fail.val != 0f ||
                state.stencilOpFront.zFail.val != 0f ||
                state.stencilOpFront.comp.val != 8f)
            {
                sb.Append(ConvertSerializedStencilOp(state.stencilOpFront, "Front"));
            }
            if (state.stencilOpBack.pass.val != 0f ||
                state.stencilOpBack.fail.val != 0f ||
                state.stencilOpBack.zFail.val != 0f ||
                state.stencilOpBack.comp.val != 8f)
            {
                sb.Append(ConvertSerializedStencilOp(state.stencilOpBack, "Back"));
            }
            sb.Append("  }\n");
        }

        if (state.fogMode != FogMode.Unknown ||
            state.fogColor.x.val != 0f ||
            state.fogColor.y.val != 0f ||
            state.fogColor.z.val != 0f ||
            state.fogColor.w.val != 0f ||
            state.fogDensity.val != 0f ||
            state.fogStart.val != 0f ||
            state.fogEnd.val != 0f)
        {
            sb.Append("  Fog {\n");
            if (state.fogMode != FogMode.Unknown)
            {
                sb.Append("   Mode ");
                switch (state.fogMode)
                {
                    case FogMode.Disabled:
                        sb.Append("Off");
                        break;
                    case FogMode.Linear:
                        sb.Append("Linear");
                        break;
                    case FogMode.Exp:
                        sb.Append("Exp");
                        break;
                    case FogMode.Exp2:
                        sb.Append("Exp2");
                        break;
                }
                sb.Append("\n");
            }
            if (state.fogColor.x.val != 0f ||
                state.fogColor.y.val != 0f ||
                state.fogColor.z.val != 0f ||
                state.fogColor.w.val != 0f)
            {
                sb.AppendFormat("   Color ({0},{1},{2},{3})\n",
                    state.fogColor.x.val.ToString(CultureInfo.InvariantCulture),
                    state.fogColor.y.val.ToString(CultureInfo.InvariantCulture),
                    state.fogColor.z.val.ToString(CultureInfo.InvariantCulture),
                    state.fogColor.w.val.ToString(CultureInfo.InvariantCulture));
            }
            if (state.fogDensity.val != 0f)
            {
                sb.Append($"   Density {state.fogDensity.val.ToString(CultureInfo.InvariantCulture)}\n");
            }
            if (state.fogStart.val != 0f ||
                state.fogEnd.val != 0f)
            {
                sb.Append($"   Range {state.fogStart.val.ToString(CultureInfo.InvariantCulture)}, {state.fogEnd.val.ToString(CultureInfo.InvariantCulture)}\n");
            }
            sb.Append("  }\n");
        }

        if (state.lighting)
        {
            sb.Append($"  Lighting {(state.lighting ? "On" : "Off")}\n");
        }

        sb.Append($"  GpuProgramID {state.gpuProgramID}\n");

        return sb.ToString();
    }

    private static string ConvertSerializedStencilOp(SerializedStencilOp stencilOp, string suffix)
    {
        var sb = new StringBuilder();
        sb.Append($"   Comp{suffix} {ConvertStencilComp(stencilOp.comp)}\n");
        sb.Append($"   Pass{suffix} {ConvertStencilOp(stencilOp.pass)}\n");
        sb.Append($"   Fail{suffix} {ConvertStencilOp(stencilOp.fail)}\n");
        sb.Append($"   ZFail{suffix} {ConvertStencilOp(stencilOp.zFail)}\n");
        return sb.ToString();
    }

    private static string ConvertStencilOp(SerializedShaderFloatValue op)
    {
        switch (op.val)
        {
            case 0f:
            default:
                return "Keep";
            case 1f:
                return "Zero";
            case 2f:
                return "Replace";
            case 3f:
                return "IncrSat";
            case 4f:
                return "DecrSat";
            case 5f:
                return "Invert";
            case 6f:
                return "IncrWrap";
            case 7f:
                return "DecrWrap";
        }
    }

    private static string ConvertStencilComp(SerializedShaderFloatValue comp)
    {
        switch (comp.val)
        {
            case 0f:
                return "Disabled";
            case 1f:
                return "Never";
            case 2f:
                return "Less";
            case 3f:
                return "Equal";
            case 4f:
                return "LEqual";
            case 5f:
                return "Greater";
            case 6f:
                return "NotEqual";
            case 7f:
                return "GEqual";
            case 8f:
            default:
                return "Always";
        }
    }

    private static string ConvertSerializedShaderRTBlendState(SerializedShaderRTBlendState[] rtBlend, bool rtSeparateBlend)
    {
        var sb = new StringBuilder();
        for (var i = 0; i < rtBlend.Length; i++)
        {
            var blend = rtBlend[i];
            if (blend.srcBlend.val != 1f ||
                blend.destBlend.val != 0f ||
                blend.srcBlendAlpha.val != 1f ||
                blend.destBlendAlpha.val != 0f)
            {
                sb.Append("  Blend ");
                if (i != 0 || rtSeparateBlend)
                {
                    sb.Append($"{i} ");
                }
                sb.Append($"{ConvertBlendFactor(blend.srcBlend)} {ConvertBlendFactor(blend.destBlend)}");
                if (blend.srcBlendAlpha.val != 1f ||
                    blend.destBlendAlpha.val != 0f)
                {
                    sb.Append($", {ConvertBlendFactor(blend.srcBlendAlpha)} {ConvertBlendFactor(blend.destBlendAlpha)}");
                }
                sb.Append("\n");
            }

            if (blend.blendOp.val != 0f ||
                blend.blendOpAlpha.val != 0f)
            {
                sb.Append("  BlendOp ");
                if (i != 0 || rtSeparateBlend)
                {
                    sb.Append($"{i} ");
                }
                sb.Append(ConvertBlendOp(blend.blendOp));
                if (blend.blendOpAlpha.val != 0f)
                {
                    sb.Append($", {ConvertBlendOp(blend.blendOpAlpha)}");
                }
                sb.Append("\n");
            }

            var val = (int)blend.colMask.val;
            if (val != 0xf)
            {
                sb.Append("  ColorMask ");
                if (val == 0)
                {
                    sb.Append(0);
                }
                else
                {
                    if ((val & 0x2) != 0)
                    {
                        sb.Append("R");
                    }
                    if ((val & 0x4) != 0)
                    {
                        sb.Append("G");
                    }
                    if ((val & 0x8) != 0)
                    {
                        sb.Append("B");
                    }
                    if ((val & 0x1) != 0)
                    {
                        sb.Append("A");
                    }
                }
                sb.Append($" {i}\n");
            }
        }
        return sb.ToString();
    }

    private static string ConvertBlendOp(SerializedShaderFloatValue op)
    {
        switch (op.val)
        {
            case 0f:
            default:
                return "Add";
            case 1f:
                return "Sub";
            case 2f:
                return "RevSub";
            case 3f:
                return "Min";
            case 4f:
                return "Max";
            case 5f:
                return "LogicalClear";
            case 6f:
                return "LogicalSet";
            case 7f:
                return "LogicalCopy";
            case 8f:
                return "LogicalCopyInverted";
            case 9f:
                return "LogicalNoop";
            case 10f:
                return "LogicalInvert";
            case 11f:
                return "LogicalAnd";
            case 12f:
                return "LogicalNand";
            case 13f:
                return "LogicalOr";
            case 14f:
                return "LogicalNor";
            case 15f:
                return "LogicalXor";
            case 16f:
                return "LogicalEquiv";
            case 17f:
                return "LogicalAndReverse";
            case 18f:
                return "LogicalAndInverted";
            case 19f:
                return "LogicalOrReverse";
            case 20f:
                return "LogicalOrInverted";
        }
    }

    private static string ConvertBlendFactor(SerializedShaderFloatValue factor)
    {
        switch (factor.val)
        {
            case 0f:
                return "Zero";
            case 1f:
            default:
                return "One";
            case 2f:
                return "DstColor";
            case 3f:
                return "SrcColor";
            case 4f:
                return "OneMinusDstColor";
            case 5f:
                return "SrcAlpha";
            case 6f:
                return "OneMinusSrcColor";
            case 7f:
                return "DstAlpha";
            case 8f:
                return "OneMinusDstAlpha";
            case 9f:
                return "SrcAlphaSaturate";
            case 10f:
                return "OneMinusSrcAlpha";
        }
    }

    private static string ConvertSerializedTagMap(SerializedTagMap tags, int intent)
    {
        var sb = new StringBuilder();
        if (tags.tags.Length > 0)
        {
            sb.Append(new string(' ', intent));
            sb.Append("Tags { ");
            foreach (var pair in tags.tags)
            {
                sb.Append($"\"{pair.Key}\" = \"{pair.Value}\" ");
            }
            sb.Append("}\n");
        }
        return sb.ToString();
    }

    private static string ConvertSerializedProperties(SerializedProperties propInfo)
    {
        var sb = new StringBuilder();
        sb.Append("Properties {\n");
        foreach (var m_Prop in propInfo.m_Props)
        {
            sb.Append(ConvertSerializedProperty(m_Prop));
        }
        sb.Append("}\n");
        return sb.ToString();
    }

    private static string ConvertSerializedProperty(SerializedProperty prop)
    {
        var sb = new StringBuilder();
        foreach (var attribute in prop.m_Attributes)
        {
            sb.Append($"[{attribute}] ");
        }
        //TODO Flag
        sb.Append($"{prop.m_Name} (\"{prop.m_Description}\", ");
        switch (prop.m_Type)
        {
            case SerializedPropertyType.Color:
                sb.Append("Color");
                break;
            case SerializedPropertyType.Vector:
                sb.Append("Vector");
                break;
            case SerializedPropertyType.Float:
                sb.Append("Float");
                break;
            case SerializedPropertyType.Range:
                sb.Append($"Range({prop.m_DefValue[1]}, {prop.m_DefValue[2]})");
                break;
            case SerializedPropertyType.Texture:
                switch (prop.m_DefTexture.m_TexDim)
                {
                    case TextureDimension.Any:
                        sb.Append("any");
                        break;
                    case TextureDimension.Tex2D:
                        sb.Append("2D");
                        break;
                    case TextureDimension.Tex3D:
                        sb.Append("3D");
                        break;
                    case TextureDimension.Cube:
                        sb.Append("Cube");
                        break;
                    case TextureDimension.Tex2DArray:
                        sb.Append("2DArray");
                        break;
                    case TextureDimension.CubeArray:
                        sb.Append("CubeArray");
                        break;
                }
                break;
        }
        sb.Append(") = ");
        switch (prop.m_Type)
        {
            case SerializedPropertyType.Color:
            case SerializedPropertyType.Vector:
                sb.Append($"({prop.m_DefValue[0]},{prop.m_DefValue[1]},{prop.m_DefValue[2]},{prop.m_DefValue[3]})");
                break;
            case SerializedPropertyType.Float:
            case SerializedPropertyType.Range:
                sb.Append(prop.m_DefValue[0]);
                break;
            case SerializedPropertyType.Texture:
                sb.Append($"\"{prop.m_DefTexture.m_DefaultName}\" {{ }}");
                break;
            default:
                throw new ArgumentOutOfRangeException();
        }
        sb.Append("\n");
        return sb.ToString();
    }

    private static bool CheckGpuProgramUsable(ShaderCompilerPlatform platform, ShaderGpuProgramType programType)
    {
        switch (platform)
        {
            case ShaderCompilerPlatform.GL:
                return programType == ShaderGpuProgramType.GLLegacy;
            case ShaderCompilerPlatform.D3D9:
                return programType == ShaderGpuProgramType.DX9VertexSM20
                       || programType == ShaderGpuProgramType.DX9VertexSM30
                       || programType == ShaderGpuProgramType.DX9PixelSM20
                       || programType == ShaderGpuProgramType.DX9PixelSM30;
            case ShaderCompilerPlatform.Xbox360:
            case ShaderCompilerPlatform.PS3:
            case ShaderCompilerPlatform.PSP2:
            case ShaderCompilerPlatform.PS4:
            case ShaderCompilerPlatform.XboxOne:
            case ShaderCompilerPlatform.N3DS:
            case ShaderCompilerPlatform.WiiU:
            case ShaderCompilerPlatform.Switch:
            case ShaderCompilerPlatform.XboxOneD3D12:
            case ShaderCompilerPlatform.GameCoreXboxOne:
            case ShaderCompilerPlatform.GameCoreScarlett:
            case ShaderCompilerPlatform.PS5:
                return programType == ShaderGpuProgramType.ConsoleVS
                       || programType == ShaderGpuProgramType.ConsoleFS
                       || programType == ShaderGpuProgramType.ConsoleHS
                       || programType == ShaderGpuProgramType.ConsoleDS
                       || programType == ShaderGpuProgramType.ConsoleGS;
            case ShaderCompilerPlatform.PS5NGGC:
                return programType == ShaderGpuProgramType.PS5NGGC;
            case ShaderCompilerPlatform.D3D11:
                return programType == ShaderGpuProgramType.DX11VertexSM40
                       || programType == ShaderGpuProgramType.DX11VertexSM50
                       || programType == ShaderGpuProgramType.DX11PixelSM40
                       || programType == ShaderGpuProgramType.DX11PixelSM50
                       || programType == ShaderGpuProgramType.DX11GeometrySM40
                       || programType == ShaderGpuProgramType.DX11GeometrySM50
                       || programType == ShaderGpuProgramType.DX11HullSM50
                       || programType == ShaderGpuProgramType.DX11DomainSM50;
            case ShaderCompilerPlatform.GLES20:
                return programType == ShaderGpuProgramType.GLES;
            case ShaderCompilerPlatform.NaCl: //Obsolete
                throw new NotSupportedException();
            case ShaderCompilerPlatform.Flash: //Obsolete
                throw new NotSupportedException();
            case ShaderCompilerPlatform.D3D11_9x:
                return programType == ShaderGpuProgramType.DX10Level9Vertex
                       || programType == ShaderGpuProgramType.DX10Level9Pixel;
            case ShaderCompilerPlatform.GLES3Plus:
                return programType == ShaderGpuProgramType.GLES31AEP
                       || programType == ShaderGpuProgramType.GLES31
                       || programType == ShaderGpuProgramType.GLES3;
            case ShaderCompilerPlatform.PSM: //Unknown
                throw new NotSupportedException();
            case ShaderCompilerPlatform.Metal:
                return programType == ShaderGpuProgramType.MetalVS
                       || programType == ShaderGpuProgramType.MetalFS;
            case ShaderCompilerPlatform.OpenGLCore:
                return programType == ShaderGpuProgramType.GLCore32
                       || programType == ShaderGpuProgramType.GLCore41
                       || programType == ShaderGpuProgramType.GLCore43;
            case ShaderCompilerPlatform.Vulkan:
                return programType == ShaderGpuProgramType.SPIRV;
            default:
                throw new NotSupportedException();
        }
    }

    public static string GetPlatformString(ShaderCompilerPlatform platform)
    {
        switch (platform)
        {
            case ShaderCompilerPlatform.GL:
                return "openGL";
            case ShaderCompilerPlatform.D3D9:
                return "d3d9";
            case ShaderCompilerPlatform.Xbox360:
                return "xbox360";
            case ShaderCompilerPlatform.PS3:
                return "ps3";
            case ShaderCompilerPlatform.D3D11:
                return "d3d11";
            case ShaderCompilerPlatform.GLES20:
                return "gles";
            case ShaderCompilerPlatform.NaCl:
                return "glesdesktop";
            case ShaderCompilerPlatform.Flash:
                return "flash";
            case ShaderCompilerPlatform.D3D11_9x:
                return "d3d11_9x";
            case ShaderCompilerPlatform.GLES3Plus:
                return "gles3";
            case ShaderCompilerPlatform.PSP2:
                return "psp2";
            case ShaderCompilerPlatform.PS4:
                return "ps4";
            case ShaderCompilerPlatform.XboxOne:
                return "xboxone";
            case ShaderCompilerPlatform.PSM:
                return "psm";
            case ShaderCompilerPlatform.Metal:
                return "metal";
            case ShaderCompilerPlatform.OpenGLCore:
                return "glcore";
            case ShaderCompilerPlatform.N3DS:
                return "n3ds";
            case ShaderCompilerPlatform.WiiU:
                return "wiiu";
            case ShaderCompilerPlatform.Vulkan:
                return "vulkan";
            case ShaderCompilerPlatform.Switch:
                return "switch";
            case ShaderCompilerPlatform.XboxOneD3D12:
                return "xboxone_d3d12";
            case ShaderCompilerPlatform.GameCoreXboxOne:
                return "xboxone";
            case ShaderCompilerPlatform.GameCoreScarlett:
                return "xbox_scarlett";
            case ShaderCompilerPlatform.PS5:
                return "ps5";
            case ShaderCompilerPlatform.PS5NGGC:
                return "ps5_nggc";
            default:
                return "unknown";
        }
    }

    private static string header = "//////////////////////////////////////////\n" +
                                   "//\n" +
                                   "// NOTE: This is *not* a valid shader file\n" +
                                   "//\n" +
                                   "///////////////////////////////////////////\n";
}

public class ShaderSubProgramEntry
{
    public int Offset;
    public int Length;
    public int Segment;

    public ShaderSubProgramEntry(BinaryReader reader, int[] version)
    {
        Offset = reader.ReadInt32();
        Length = reader.ReadInt32();
        if (version[0] > 2019 || (version[0] == 2019 && version[1] >= 3)) //2019.3 and up
        {
            Segment = reader.ReadInt32();
        }
    }
}

public class ShaderProgram
{
    public ShaderSubProgramEntry[] Entries;
    public ShaderSubProgram[] SubPrograms;

    public ShaderProgram(BinaryReader reader, int[] version)
    {
        var subProgramsCapacity = reader.ReadInt32();
        Entries = new ShaderSubProgramEntry[subProgramsCapacity];
        for (int i = 0; i < subProgramsCapacity; i++)
        {
            Entries[i] = new ShaderSubProgramEntry(reader, version);
        }
        SubPrograms = new ShaderSubProgram[subProgramsCapacity];
    }

    public void Read(BinaryReader reader, int segment)
    {
        for (int i = 0; i < Entries.Length; i++)
        {
            var entry = Entries[i];
            if (entry.Segment == segment)
            {
                reader.BaseStream.Position = entry.Offset;
                SubPrograms[i] = new ShaderSubProgram(reader);
            }
        }
    }

    public string Export(string shader)
    {
        var evaluator = new MatchEvaluator(match =>
        {
            var index = int.Parse(match.Groups[1].Value);
            return SubPrograms[index].Export();
        });
        shader = Regex.Replace(shader, "GpuProgramIndex (.+)", evaluator);
        return shader;
    }
}

public class ShaderSubProgram
{
    private int m_Version;
    public ShaderGpuProgramType ProgramType;
    public string[] Keywords;
    public string[] LocalKeywords;
    public byte[] ProgramCode;

    public ShaderSubProgram(BinaryReader reader)
    {
        //LoadGpuProgramFromData
        //201509030 - Unity 5.3
        //201510240 - Unity 5.4
        //201608170 - Unity 5.5
        //201609010 - Unity 5.6, 2017.1 & 2017.2
        //201708220 - Unity 2017.3, Unity 2017.4 & Unity 2018.1
        //201802150 - Unity 2018.2 & Unity 2018.3
        //201806140 - Unity 2019.1~2021.1
        //202012090 - Unity 2021.2
        m_Version = reader.ReadInt32();
        ProgramType = (ShaderGpuProgramType)reader.ReadInt32();
        reader.BaseStream.Position += 12;
        if (m_Version >= 201608170)
        {
            reader.BaseStream.Position += 4;
        }
        var keywordsSize = reader.ReadInt32();
        Keywords = new string[keywordsSize];
        for (int i = 0; i < keywordsSize; i++)
        {
            Keywords[i] = reader.ReadAlignedString();
        }
        if (m_Version >= 201806140 && m_Version < 202012090)
        {
            var localKeywordsSize = reader.ReadInt32();
            LocalKeywords = new string[localKeywordsSize];
            for (int i = 0; i < localKeywordsSize; i++)
            {
                LocalKeywords[i] = reader.ReadAlignedString();
            }
        }
        ProgramCode = reader.ReadUInt8Array();
        reader.AlignStream();

        //TODO
    }

    public string Export()
    {
        var sb = new StringBuilder();
        if (Keywords.Length > 0)
        {
            sb.Append("Keywords { ");
            foreach (string keyword in Keywords)
            {
                sb.Append($"\"{keyword}\" ");
            }
            sb.Append("}\n");
        }
        if (LocalKeywords != null && LocalKeywords.Length > 0)
        {
            sb.Append("Local Keywords { ");
            foreach (string keyword in LocalKeywords)
            {
                sb.Append($"\"{keyword}\" ");
            }
            sb.Append("}\n");
        }

        sb.Append("\"");
        if (ProgramCode.Length > 0)
        {
            switch (ProgramType)
            {
                case ShaderGpuProgramType.GLLegacy:
                case ShaderGpuProgramType.GLES31AEP:
                case ShaderGpuProgramType.GLES31:
                case ShaderGpuProgramType.GLES3:
                case ShaderGpuProgramType.GLES:
                case ShaderGpuProgramType.GLCore32:
                case ShaderGpuProgramType.GLCore41:
                case ShaderGpuProgramType.GLCore43:
                    sb.Append(Encoding.UTF8.GetString(ProgramCode));
                    break;
                case ShaderGpuProgramType.DX9VertexSM20:
                case ShaderGpuProgramType.DX9VertexSM30:
                case ShaderGpuProgramType.DX9PixelSM20:
                case ShaderGpuProgramType.DX9PixelSM30:
                {
                    /*var shaderBytecode = new ShaderBytecode(m_ProgramCode);
                    sb.Append(shaderBytecode.Disassemble());*/
                    sb.Append("// shader disassembly not supported on DXBC");
                    break;
                }
                case ShaderGpuProgramType.DX10Level9Vertex:
                case ShaderGpuProgramType.DX10Level9Pixel:
                case ShaderGpuProgramType.DX11VertexSM40:
                case ShaderGpuProgramType.DX11VertexSM50:
                case ShaderGpuProgramType.DX11PixelSM40:
                case ShaderGpuProgramType.DX11PixelSM50:
                case ShaderGpuProgramType.DX11GeometrySM40:
                case ShaderGpuProgramType.DX11GeometrySM50:
                case ShaderGpuProgramType.DX11HullSM50:
                case ShaderGpuProgramType.DX11DomainSM50:
                {
                    /*int start = 6;
                    if (m_Version == 201509030) // 5.3
                    {
                        start = 5;
                    }
                    var buff = new byte[m_ProgramCode.Length - start];
                    Buffer.BlockCopy(m_ProgramCode, start, buff, 0, buff.Length);
                    var shaderBytecode = new ShaderBytecode(buff);
                    sb.Append(shaderBytecode.Disassemble());*/
                    sb.Append("// shader disassembly not supported on DXBC");
                    break;
                }
                case ShaderGpuProgramType.MetalVS:
                case ShaderGpuProgramType.MetalFS:
                    using (var reader = new BinaryReader(new MemoryStream(ProgramCode)))
                    {
                        var fourCC = reader.ReadUInt32();
                        if (fourCC == 0xf00dcafe)
                        {
                            int offset = reader.ReadInt32();
                            reader.BaseStream.Position = offset;
                        }
                        var entryName = reader.ReadStringToNull();
                        var buff = reader.ReadBytes((int)(reader.BaseStream.Length - reader.BaseStream.Position));
                        sb.Append(Encoding.UTF8.GetString(buff));
                    }
                    break;
                case ShaderGpuProgramType.SPIRV:
                    try
                    {
                        sb.Append(SpirVShaderConverter.Convert(ProgramCode));
                    }
                    catch (Exception e)
                    {
                        sb.Append($"// disassembly error {e.Message}\n");
                    }
                    break;
                case ShaderGpuProgramType.ConsoleVS:
                case ShaderGpuProgramType.ConsoleFS:
                case ShaderGpuProgramType.ConsoleHS:
                case ShaderGpuProgramType.ConsoleDS:
                case ShaderGpuProgramType.ConsoleGS:
                    sb.Append(Encoding.UTF8.GetString(ProgramCode));
                    break;
                default:
                    sb.Append($"//shader disassembly not supported on {ProgramType}");
                    break;
            }
        }
        sb.Append('"');
        return sb.ToString();
    }
}