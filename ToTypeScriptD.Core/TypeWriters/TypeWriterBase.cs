﻿using Mono.Cecil;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace ToTypeScriptD.Core.TypeWriters
{
    public abstract class TypeWriterBase : ITypeWriter
    {
        public TypeDefinition TypeDefinition { get; set; }
        public int IndentCount { get; set; }
        public TypeCollection TypeCollection { get; set; }

        public static TypeWriterConfig config;
        public static TypeWriterConfig Config
        {
            get { return config ?? TypeWriterConfig.Instance; }
            set { config = value; }
        }

        public TypeWriterBase(TypeDefinition typeDefinition, int indentCount, TypeCollection typeCollection)
        {
            this.TypeDefinition = typeDefinition;
            this.IndentCount = indentCount;
            this.TypeCollection = typeCollection;
        }

        public virtual void Write(StringBuilder sb, Action midWrite)
        {
            midWrite();
        }
        public virtual void Write(StringBuilder sb)
        {
            //noop
        }


        public string IndentValue
        {
            get { return Config.Indentation.Dup(IndentCount); }
        }

        public void Indent(StringBuilder sb)
        {
            sb.Append(IndentValue);
        }

        internal void WriteOutMethodSignatures(StringBuilder sb, string exportType, string inheriterString)
        {
            List<ITypeWriter> extendedTypes = new List<ITypeWriter>();
            Indent(sb); sb.AppendFormat("export {0} {1}", exportType, TypeDefinition.ToTypeScriptItemName());

            if (TypeDefinition.HasGenericParameters)
            {
                sb.Append("<");
                TypeDefinition.GenericParameters.For((genericParameter, i, isLastItem) =>
                {
                    sb.AppendFormat("{0}{1}", genericParameter.ToTypeScriptType(), isLastItem ? "" : ",");
                });
                sb.Append(">");
            }

            sb.Append(" ");
            if (TypeDefinition.Interfaces.Any())
            {
                var interfaceTypes = TypeDefinition.Interfaces.Where(w => !w.Name.ShouldIgnoreTypeByName());
                if (interfaceTypes.Any())
                {
                    sb.Append(inheriterString);
                    interfaceTypes.For((item, i, isLast) =>
                    {
                        sb.AppendFormat(" {0}{1}", item.ToTypeScriptType(), isLast ? " " : ",");
                    });
                }
            }
            sb.AppendLine("{");

            // TODO: get specific types of EventListener types?
            if (TypeDefinition.HasEvents)
            {
                Indent(sb); Indent(sb); sb.AppendLine("addEventListener(type: string, listener: EventListener): void;");
                Indent(sb); Indent(sb); sb.AppendLine("removeEventListener(type: string, listener: EventListener): void;");

                TypeDefinition.Events.For((item, i, isLast) =>
                {
                    // TODO: events with multiple return types???
                    Indent(sb); Indent(sb); sb.AppendLine("on" + item.Name.ToLower() + "(ev: any);");
                });
            }

            TypeDefinition.Fields.Each(field =>
            {
                if (!field.IsPublic) return;
                var fieldName = field.Name.ToTypeScriptName();
                Indent(sb); Indent(sb); sb.AppendFormat("{0}: {1};", fieldName, field.FieldType.ToTypeScriptType());
                sb.AppendLine();
            });

            var propNames = new HashSet<string>();
            TypeDefinition.Properties.Each(prop =>
            {
                // TODO: determine if property was already defined by interface?

                var propName = prop.Name.ToTypeScriptName();
                propNames.Add(propName);
                Indent(sb); Indent(sb); sb.AppendFormat("{0}{1}: {2};", propName, prop.PropertyType.ToTypeScriptNullable(), prop.PropertyType.ToTypeScriptType());
                sb.AppendLine();
            });

            var methodSignatures = new HashSet<string>();

            foreach (var method in TypeDefinition.Methods)
            {
                var methodSb = new StringBuilder();
                // TODO: determine if method was already defined by interface?

                var methodName = method.Name;

                // ignore special event handler methods
                if (method.HasParameters &&
                    method.Parameters[0].Name.StartsWith("__param0") &&
                    (methodName.StartsWith("add_") || methodName.StartsWith("remove_")))
                    continue;

                // already handled properties
                if (method.IsGetter || method.IsSetter)
                    continue;

                // translate the constructor function
                if (method.IsConstructor)
                {
                    methodName = "constructor";
                }

                // Lowercase first char of the method
                methodName = methodName.ToTypeScriptName();

                Indent(methodSb); Indent(methodSb);
                if (method.IsStatic)
                {
                    methodSb.Append("static ");
                }
                methodSb.Append(methodName);

                var outTypes = new List<ParameterDefinition>();

                methodSb.Append("(");
                method.Parameters.Where(w => w.IsOut).Each(e => outTypes.Add(e));
                method.Parameters.Where(w => !w.IsOut).For((parameter, i, isLast) =>
                {
                    methodSb.AppendFormat("{0}{1}{2}: {3}{4}",
                        (i == 0 ? "" : " "),                            // spacer
                        parameter.Name,                                 // argument name
                        parameter.ParameterType.ToTypeScriptNullable(), // nullable
                        parameter.ParameterType.ToTypeScriptType(),     // type
                        (isLast ? "" : ","));                           // last one gets a comma
                });
                methodSb.Append(")");

                // constructors don't have return types.
                if (!method.IsConstructor)
                {
                    string returnType;
                    if (outTypes.Any())
                    {
                        var outWriter = new OutParameterReturnTypeWriter(IndentCount, TypeDefinition, methodName, method.ReturnType, outTypes);
                        extendedTypes.Add(outWriter);
                        //TypeCollection.Add(TypeDefinition.Namespace, outWriter.TypeName, outWriter);
                        returnType = outWriter.TypeName;
                    }
                    else
                    {
                        returnType = method.ReturnType.ToTypeScriptType();
                    }

                    methodSb.AppendFormat(": {0}", returnType);
                }
                methodSb.AppendLine(";");

                var renderedMethod = methodSb.ToString();
                if (!methodSignatures.Contains(renderedMethod))
                    methodSignatures.Add(renderedMethod);
            }

            methodSignatures.Each(method => sb.Append(method));

            Indent(sb); sb.AppendLine("}");

            extendedTypes.Each(item => item.Write(sb));

            TypeDefinition.NestedTypes.Where(type => type.IsNestedPublic).Each(type =>
            {
                var typeWriter = TypeWriterCollector.PickTypeWriter(type, IndentCount-1, TypeCollection);
                sb.AppendLine();
                typeWriter.Write(sb);
            });
        }
    }
}
