﻿//------------------------------------------------------------------------------
// <auto-generated>
//     此代码由工具生成。
//     运行时版本:4.0.30319.42000
//
//     对此文件的更改可能会导致不正确的行为，并且如果
//     重新生成代码，这些更改将会丢失。
// </auto-generated>
//------------------------------------------------------------------------------

namespace AssetStudioGUI.Properties {
    using System;
    
    
    /// <summary>
    ///   一个强类型的资源类，用于查找本地化的字符串等。
    /// </summary>
    // 此类是由 StronglyTypedResourceBuilder
    // 类通过类似于 ResGen 或 Visual Studio 的工具自动生成的。
    // 若要添加或移除成员，请编辑 .ResX 文件，然后重新运行 ResGen
    // (以 /str 作为命令选项)，或重新生成 VS 项目。
    [global::System.CodeDom.Compiler.GeneratedCodeAttribute("System.Resources.Tools.StronglyTypedResourceBuilder", "16.0.0.0")]
    [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
    [global::System.Runtime.CompilerServices.CompilerGeneratedAttribute()]
    internal class Resources {
        
        private static global::System.Resources.ResourceManager resourceMan;
        
        private static global::System.Globalization.CultureInfo resourceCulture;
        
        [global::System.Diagnostics.CodeAnalysis.SuppressMessageAttribute("Microsoft.Performance", "CA1811:AvoidUncalledPrivateCode")]
        internal Resources() {
        }
        
        /// <summary>
        ///   返回此类使用的缓存的 ResourceManager 实例。
        /// </summary>
        [global::System.ComponentModel.EditorBrowsableAttribute(global::System.ComponentModel.EditorBrowsableState.Advanced)]
        internal static global::System.Resources.ResourceManager ResourceManager {
            get {
                if (object.ReferenceEquals(resourceMan, null)) {
                    global::System.Resources.ResourceManager temp = new global::System.Resources.ResourceManager("AssetStudioGUI.Properties.Resources", typeof(Resources).Assembly);
                    resourceMan = temp;
                }
                return resourceMan;
            }
        }
        
        /// <summary>
        ///   重写当前线程的 CurrentUICulture 属性，对
        ///   使用此强类型资源类的所有资源查找执行重写。
        /// </summary>
        [global::System.ComponentModel.EditorBrowsableAttribute(global::System.ComponentModel.EditorBrowsableState.Advanced)]
        internal static global::System.Globalization.CultureInfo Culture {
            get {
                return resourceCulture;
            }
            set {
                resourceCulture = value;
            }
        }
        
        /// <summary>
        ///   查找类似于 (图标) 的 System.Drawing.Icon 类型的本地化资源。
        /// </summary>
        internal static System.Drawing.Icon _as {
            get {
                object obj = ResourceManager.GetObject("_as", resourceCulture);
                return ((System.Drawing.Icon)(obj));
            }
        }
        
        /// <summary>
        ///   查找类似 #version 140
        ///
        ///in vec3 normal;
        ///
        ///out vec4 outputColor;
        ///
        ///void main()
        ///{
        ///	vec3 unitNormal = normalize(normal);
        ///	float nDotProduct = clamp(dot(unitNormal, vec3(0.707, 0, 0.707)), 0, 1);
        ///	vec2 ContributionWeightsSqrt = vec2(0.5, 0.5f) + vec2(0.5f, -0.5f) * unitNormal.y;
        ///	vec2 ContributionWeights = ContributionWeightsSqrt * ContributionWeightsSqrt;
        ///
        ///	vec3 color = nDotProduct * vec3(1, 0.957, 0.839) / 3.14159;
        ///	color += vec3(0.779, 0.716, 0.453) * ContributionWeights.y;
        ///	color += vec3(0.368, 0.477, 0. [字符串的其余部分被截断]&quot;; 的本地化字符串。
        /// </summary>
        internal static string fs {
            get {
                return ResourceManager.GetString("fs", resourceCulture);
            }
        }
        
        /// <summary>
        ///   查找类似 #version 140
        ///
        ///out vec4 outputColor;
        ///
        ///void main()
        ///{
        ///	outputColor = vec4(0, 0, 0, 1);
        ///} 的本地化字符串。
        /// </summary>
        internal static string fsBlack {
            get {
                return ResourceManager.GetString("fsBlack", resourceCulture);
            }
        }
        
        /// <summary>
        ///   查找类似 #version 140
        ///
        ///out vec4 outputColor;
        ///in vec4 color;
        ///
        ///void main()
        ///{
        ///	outputColor = color;
        ///} 的本地化字符串。
        /// </summary>
        internal static string fsColor {
            get {
                return ResourceManager.GetString("fsColor", resourceCulture);
            }
        }
        
        /// <summary>
        ///   查找 System.Drawing.Bitmap 类型的本地化资源。
        /// </summary>
        internal static System.Drawing.Bitmap preview {
            get {
                object obj = ResourceManager.GetObject("preview", resourceCulture);
                return ((System.Drawing.Bitmap)(obj));
            }
        }
        
        /// <summary>
        ///   查找类似 #version 140
        ///
        ///in vec3 vertexPosition;
        ///in vec3 normalDirection;
        ///in vec4 vertexColor;
        ///uniform mat4 modelMatrix;
        ///uniform mat4 viewMatrix;
        ///uniform mat4 projMatrix;
        ///
        ///out vec3 normal;
        ///out vec4 color;
        ///
        ///void main()
        ///{
        ///	gl_Position = projMatrix * viewMatrix * modelMatrix * vec4(vertexPosition, 1.0);
        ///	normal = normalDirection;
        ///	color = vertexColor; 
        ///} 的本地化字符串。
        /// </summary>
        internal static string vs {
            get {
                return ResourceManager.GetString("vs", resourceCulture);
            }
        }
    }
}
