﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using ToTypeScriptD.Core.Config;
using ToTypeScriptD.Core.Extensions;
using ToTypeScriptD.Lexical.DotNet;
using ToTypeScriptD.Lexical.Extensions;
using ToTypeScriptD.Lexical.TypeWriters;

namespace ToTypeScriptD.Lexical.WinMD
{
    public abstract class TypeWriterBase : ITypeWriter
    {
        public abstract void Write(System.Text.StringBuilder sb);

        public readonly Type TypeDefinition;
        public readonly ConfigBase Config;

        public ITypeWriterTypeSelector TypeSelector { get; private set; }
        

        public string FullName => TypeDefinition.Namespace + "." + TypeDefinition.ToTypeScriptItemName();
        public void Indent(StringBuilder sb) => sb.Append(IndentValue);
        protected int IndentCount;
        public string IndentValue => Config.Indent.Dup(IndentCount);




        protected TypeWriterBase(Type typeDefinition, int indentCount, ConfigBase config, ITypeWriterTypeSelector typeSelector)
        {
            this.TypeDefinition = typeDefinition;
            this.IndentCount = indentCount;
            this.Config = config;
            this.TypeSelector = typeSelector;
        }

        
        public void WriteOutMethodSignatures(StringBuilder sb, string exportType, string inheriterString)
        {
            Indent(sb); sb.AppendFormat("export {0} {1}", exportType, TypeDefinition.ToTypeScriptItemNameWinMD());
            WriteGenerics(sb);
            sb.Append(" ");
            WriteExportedInterfaces(sb, inheriterString);
            sb.AppendLine("{");
            bool wroteALengthProperty;

            List<ITypeWriter> extendedTypes = WriteMethods(sb);
            WriteFields(sb);
            WriteProperties(sb, out wroteALengthProperty);
            WriteEvents(sb);

            Indent(sb); sb.AppendLine("}");

            WriteExtendedTypes(sb, extendedTypes);
            WriteNestedTypes(sb);
        }


        private void WriteGenerics(StringBuilder sb)
        {
            if (TypeDefinition.GetGenericArguments().Any())
            {
                sb.Append("<");
                TypeDefinition.GetGenericArguments().For((genericParameter, i, isLastItem) =>
                {
                    StringBuilder constraintsSB = new StringBuilder();
                    genericParameter.GetGenericParameterConstraints().For((constraint, j, isLastItemJ) =>
                    {
                        // Not sure how best to deal with multiple generic constraints (yet)
                        // For now place in a comment
                        // TODO: possible generate a new interface type that extends all of the constraints?
                        var isFirstItem = j == 0;
                        var isOnlyItem = isFirstItem && isLastItemJ;
                        if (isOnlyItem)
                        {
                            constraintsSB.AppendFormat(" extends {0}", constraint.ToTypeScriptTypeName());
                        }
                        else
                        {
                            if (isFirstItem)
                            {
                                constraintsSB.AppendFormat(" extends {0} /*TODO:{1}", constraint.ToTypeScriptTypeName(), (isLastItemJ ? "*/" : ", "));
                            }
                            else
                            {
                                constraintsSB.AppendFormat("{0}{1}", constraint.ToTypeScriptTypeName(), (isLastItemJ ? "*/" : ", "));
                            }
                        }
                    });

                    sb.AppendFormat("{0}{1}{2}", genericParameter.ToTypeScriptTypeName(), constraintsSB.ToString(), (isLastItem ? "" : ", "));
                });
                sb.Append(">");
            }
        }


        private static void WriteExtendedTypes(StringBuilder sb, List<ITypeWriter> extendedTypes)
        {
            extendedTypes.Each(item => item.Write(sb));
        }

        private void WriteNestedTypes(StringBuilder sb)
        {
            TypeDefinition.GetNestedTypes().Where(type => type.IsNested).Each(type =>
            {
                var typeWriter = TypeSelector.PickTypeWriter(type, IndentCount - 1, Config);
                sb.AppendLine();
                typeWriter.Write(sb);
            });
        }


        private List<ITypeWriter> WriteMethods(StringBuilder sb)
        {
            List<ITypeWriter> extendedTypes = new List<ITypeWriter>();
            var methodSignatures = new HashSet<string>();
            var methods =
                TypeDefinition.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly |
                                          BindingFlags.NonPublic).Where(m => m.IsSpecialName == false && m.IsHideBySig == false);
            foreach (var method in methods)
            {
                var methodSb = new StringBuilder();

                var methodName = method.Name;

                // ignore special event handler methods
                if (method.GetParameters().Any() &&
                    method.GetParameters()[0].Name.StartsWith("__param0") &&
                    (methodName.StartsWith("add_") || methodName.StartsWith("remove_")))
                    continue;

                if (method.IsSpecialName && !method.IsConstructor)
                    continue;

                //TODO: this should be fine
                //// already handled properties
                //if (method.IsGetter || method.IsSetter)
                //    continue;

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

                var outTypes = new List<ParameterInfo>();

                methodSb.Append("(");
                method.GetParameters().Where(w => w.IsOut).Each(e => outTypes.Add(e));
                method.GetParameters().Where(w => !w.IsOut).For((parameter, i, isLast) =>
                {
                    methodSb.AppendFormat("{0}{1}: {2}{3}",
                        (i == 0 ? "" : " "),                            // spacer
                        parameter.Name,                                 // argument name
                        parameter.ParameterType.ToTypeScriptTypeName(),     // type
                        (isLast ? "" : ","));                           // last one gets a comma
                });
                methodSb.Append(")");

                // constructors don't have return types.
                if (!method.IsConstructor)
                {
                    string returnType;
                    if (outTypes.Any())
                    {
                        var outWriter = new OutParameterReturnTypeWriter(Config, IndentCount, TypeDefinition, methodName, method.ReturnType, outTypes);
                        extendedTypes.Add(outWriter);
                        //TypeCollection.Add(TypeDefinition.Namespace, outWriter.TypeName, outWriter);
                        returnType = outWriter.TypeName;
                    }
                    else
                    {
                        returnType = method.ReturnType.ToTypeScriptTypeName();
                    }

                    methodSb.AppendFormat(": {0}", returnType);
                }
                methodSb.AppendLine(";");

                var renderedMethod = methodSb.ToString();
                if (!methodSignatures.Contains(renderedMethod))
                    methodSignatures.Add(renderedMethod);
            }

            // HACK: This not a sustainable approach (but working for now)
            //       The IWebSocket inherits from IClosable and the websocket's close 
            //       conflicts with the closable close so we have to hack this method
            //       onto the websocket interface.
            if (TypeDefinition.FullName == "Windows.Networking.Sockets.IWebSocket")
            {
                methodSignatures.Add(IndentValue + IndentValue + "close(): void;" + Environment.NewLine);
            }

            methodSignatures.Each(method => sb.Append(method));

            return extendedTypes;
        }

        private void WriteProperties(StringBuilder sb, out bool wroteALengthProperty)
        {
            var wroteALengthPropertyLambdaWorkAround = false;

            TypeDefinition.GetProperties().Each(prop =>
            {
                var propName_ = prop.Name;
                var propName = propName_.ToTypeScriptName();

                
                if (propName == "length")
                {
                    wroteALengthPropertyLambdaWorkAround = true;
                }

                var propMethod = prop.GetMethod ?? prop.SetMethod;

                var staticText = propMethod.IsStatic ? "static " : "";

                Indent(sb); Indent(sb); sb.AppendFormat("{0}{1}: {2};", staticText, propName, prop.PropertyType.UnderlyingSystemType.ToTypeScriptTypeName());
                sb.AppendLine();
            });

            wroteALengthProperty = wroteALengthPropertyLambdaWorkAround;
        }

        private void WriteFields(StringBuilder sb)
        {
            TypeDefinition.GetFields(BindingFlags.DeclaredOnly | BindingFlags.Public | BindingFlags.NonPublic).Where(f => f.IsSpecialName == false).Each(field =>
            {
                if (!field.IsPublic) return;
                var fieldName = field.Name.ToTypeScriptName();
                Indent(sb); Indent(sb); sb.AppendFormat("{0}: {1};", fieldName, field.FieldType.ToTypeScriptTypeName());
                sb.AppendLine();
            });
        }

        private void WriteEvents(StringBuilder sb)
        {
            if (TypeDefinition.GetEvents().Any())
            {
                Indent(sb); Indent(sb); sb.AppendLine("// Events");

                Indent(sb); Indent(sb); sb.AppendLine("addEventListener(eventName: string, listener: any): void;");
                Indent(sb); Indent(sb); sb.AppendLine("removeEventListener(eventName: string, listener: any): void;");
                var distinctListenerSignatures = new List<string>();

                TypeDefinition.GetEvents().For((item, i, isLast) =>
                {
                    var eventListenerType = item.EventHandlerType.ToTypeScriptTypeName();
                    var eventName = item.Name.ToLower();

                    var line = IndentValue + IndentValue + "addEventListener(eventName: \"{0}\", listener: {1}): void;".FormatWith(eventName, eventListenerType);
                    if (!distinctListenerSignatures.Contains(line))
                        distinctListenerSignatures.Add(line);

                    line = IndentValue + IndentValue + "removeEventListener(eventName: \"{0}\", listener: {1}): void;".FormatWith(eventName, eventListenerType);
                    if (!distinctListenerSignatures.Contains(line))
                        distinctListenerSignatures.Add(line);

                    line = IndentValue + IndentValue + "on{0}: (ev: {1}) => void;".FormatWith(eventName, eventListenerType);
                    if (!distinctListenerSignatures.Contains(line))
                    {
                        distinctListenerSignatures.Add(line);
                    }
                });

                distinctListenerSignatures.Each(item =>
                {
                    sb.AppendLine(item);
                });
                sb.AppendLine();
            }
        }

        #region Promise Extension

        private void WriteAsyncPromiseMethods(StringBuilder sb)
        {
            string genericTypeArgName;
            if (IsTypeAsync(out genericTypeArgName))
            {
                sb.AppendLine();
                Indent(sb); Indent(sb); sb.AppendFormatLine("// Promise Extension");
                Indent(sb); Indent(sb); sb.AppendFormatLine("then<U>(success?: (value: {0}) => ToTypeScriptD.WinRT.IPromise<U>, error?: (error: any) => ToTypeScriptD.WinRT.IPromise<U>, progress?: (progress: any) => void): ToTypeScriptD.WinRT.IPromise<U>;", genericTypeArgName);
                Indent(sb); Indent(sb); sb.AppendFormatLine("then<U>(success?: (value: {0}) => ToTypeScriptD.WinRT.IPromise<U>, error?: (error: any) => U, progress?: (progress: any) => void): ToTypeScriptD.WinRT.IPromise<U>;", genericTypeArgName);
                Indent(sb); Indent(sb); sb.AppendFormatLine("then<U>(success?: (value: {0}) => U, error?: (error: any) => ToTypeScriptD.WinRT.IPromise<U>, progress?: (progress: any) => void): ToTypeScriptD.WinRT.IPromise<U>;", genericTypeArgName);
                Indent(sb); Indent(sb); sb.AppendFormatLine("then<U>(success?: (value: {0}) => U, error?: (error: any) => U, progress?: (progress: any) => void): ToTypeScriptD.WinRT.IPromise<U>;", genericTypeArgName);
                Indent(sb); Indent(sb); sb.AppendFormatLine("done<U>(success?: (value: {0}) => any, error?: (error: any) => any, progress?: (progress: any) => void): void;", genericTypeArgName);
            }
        }

        private bool IsTypeAsync(out string genericTypeArgName)
        {
            var currType = TypeDefinition;

            if (IsTypeAsync(TypeDefinition, out genericTypeArgName))
            {
                return true;
            }

            foreach (var i in TypeDefinition.GetInterfaces())
            {
                if (IsTypeAsync(i, out genericTypeArgName))
                {
                    return true;
                }
            }
            genericTypeArgName = "";
            return false;
        }

        private bool IsTypeAsync(Type typeReference, out string genericTypeArgName)
        {
            if (typeReference.FullName.StartsWith("Windows.Foundation.IAsyncOperation`1") ||
                typeReference.FullName.StartsWith("Windows.Foundation.IAsyncOperationWithProgress`2")
                )
            {
                var genericInstanceType = typeReference;// as GenericInstanceType;
                if (genericInstanceType == null)
                {
                    genericTypeArgName = "TResult";
                }
                else
                {
                    genericTypeArgName = genericInstanceType.GetGenericArguments()[0].ToTypeScriptTypeName();
                }
                return true;
            }

            genericTypeArgName = "";
            return false;
        }

        private void WriteExportedInterfaces(StringBuilder sb, string inheriterString)
        {
            if (TypeDefinition.GetInterfaces().Any())
            {
                var interfaceTypes = TypeDefinition.GetInterfaces().Where(w => !w.Name.ShouldIgnoreTypeByName());
                if (interfaceTypes.Any())
                {
                    sb.Append(inheriterString);
                    interfaceTypes.For((item, i, isLast) =>
                    {
                        sb.AppendFormat(" {0}{1}", item.ToTypeScriptTypeName(), isLast ? " " : ",");
                    });
                }
            }
        }

        #endregion

        #region Array Extension
        private void WriteVectorArrayPrototypeExtensions(StringBuilder sb, bool wroteALengthProperty)
        {
            string genericTypeArgName;
            if (IsTypeArray(out genericTypeArgName))
            {
                sb.AppendLine();
                Indent(sb); Indent(sb); sb.AppendFormatLine("// Array.prototype extensions", genericTypeArgName);
                Indent(sb); Indent(sb); sb.AppendFormatLine("toString(): string;", genericTypeArgName);
                Indent(sb); Indent(sb); sb.AppendFormatLine("toLocaleString(): string;", genericTypeArgName);
                Indent(sb); Indent(sb); sb.AppendFormatLine("concat(...items: {0}[][]): {0}[];", genericTypeArgName);
                Indent(sb); Indent(sb); sb.AppendFormatLine("join(seperator: string): string;", genericTypeArgName);
                Indent(sb); Indent(sb); sb.AppendFormatLine("pop(): {0};", genericTypeArgName);
                Indent(sb); Indent(sb); sb.AppendFormatLine("push(...items: {0}[]): void;", genericTypeArgName);
                Indent(sb); Indent(sb); sb.AppendFormatLine("reverse(): {0}[];", genericTypeArgName);
                Indent(sb); Indent(sb); sb.AppendFormatLine("shift(): {0};", genericTypeArgName);
                Indent(sb); Indent(sb); sb.AppendFormatLine("slice(start: number): {0}[];", genericTypeArgName);
                Indent(sb); Indent(sb); sb.AppendFormatLine("slice(start: number, end: number): {0}[];", genericTypeArgName);
                Indent(sb); Indent(sb); sb.AppendFormatLine("sort(): {0}[];", genericTypeArgName);
                Indent(sb); Indent(sb); sb.AppendFormatLine("sort(compareFn: (a: {0}, b: {0}) => number): {0}[];", genericTypeArgName);
                Indent(sb); Indent(sb); sb.AppendFormatLine("splice(start: number): {0}[];", genericTypeArgName);
                Indent(sb); Indent(sb); sb.AppendFormatLine("splice(start: number, deleteCount: number, ...items: {0}[]): {0}[];", genericTypeArgName);
                Indent(sb); Indent(sb); sb.AppendFormatLine("unshift(...items: {0}[]): number;", genericTypeArgName);
                Indent(sb); Indent(sb); sb.AppendFormatLine("lastIndexOf(searchElement: {0}): number;", genericTypeArgName);
                Indent(sb); Indent(sb); sb.AppendFormatLine("lastIndexOf(searchElement: {0}, fromIndex: number): number;", genericTypeArgName);
                Indent(sb); Indent(sb); sb.AppendFormatLine("every(callbackfn: (value: {0}, index: number, array: {0}[]) => boolean): boolean;", genericTypeArgName);
                Indent(sb); Indent(sb); sb.AppendFormatLine("every(callbackfn: (value: {0}, index: number, array: {0}[]) => boolean, thisArg: any): boolean;", genericTypeArgName);
                Indent(sb); Indent(sb); sb.AppendFormatLine("some(callbackfn: (value: {0}, index: number, array: {0}[]) => boolean): boolean;", genericTypeArgName);
                Indent(sb); Indent(sb); sb.AppendFormatLine("some(callbackfn: (value: {0}, index: number, array: {0}[]) => boolean, thisArg: any): boolean;", genericTypeArgName);
                Indent(sb); Indent(sb); sb.AppendFormatLine("forEach(callbackfn: (value: {0}, index: number, array: {0}[]) => void ): void;", genericTypeArgName);
                Indent(sb); Indent(sb); sb.AppendFormatLine("forEach(callbackfn: (value: {0}, index: number, array: {0}[]) => void , thisArg: any): void;", genericTypeArgName);
                Indent(sb); Indent(sb); sb.AppendFormatLine("map(callbackfn: (value: {0}, index: number, array: {0}[]) => any): any[];", genericTypeArgName);
                Indent(sb); Indent(sb); sb.AppendFormatLine("map(callbackfn: (value: {0}, index: number, array: {0}[]) => any, thisArg: any): any[];", genericTypeArgName);
                Indent(sb); Indent(sb); sb.AppendFormatLine("filter(callbackfn: (value: {0}, index: number, array: {0}[]) => boolean): {0}[];", genericTypeArgName);
                Indent(sb); Indent(sb); sb.AppendFormatLine("filter(callbackfn: (value: {0}, index: number, array: {0}[]) => boolean, thisArg: any): {0}[];", genericTypeArgName);
                Indent(sb); Indent(sb); sb.AppendFormatLine("reduce(callbackfn: (previousValue: any, currentValue: any, currentIndex: number, array: {0}[]) => any): any;", genericTypeArgName);
                Indent(sb); Indent(sb); sb.AppendFormatLine("reduce(callbackfn: (previousValue: any, currentValue: any, currentIndex: number, array: {0}[]) => any, initialValue: any): any;", genericTypeArgName);
                Indent(sb); Indent(sb); sb.AppendFormatLine("reduceRight(callbackfn: (previousValue: any, currentValue: any, currentIndex: number, array: {0}[]) => any): any;", genericTypeArgName);
                Indent(sb); Indent(sb); sb.AppendFormatLine("reduceRight(callbackfn: (previousValue: any, currentValue: any, currentIndex: number, array: {0}[]) => any, initialValue: any): any;", genericTypeArgName);
                if (!wroteALengthProperty)
                {
                    Indent(sb); Indent(sb); sb.AppendFormatLine("length: number;", genericTypeArgName);
                }

            }
        }

        private bool IsTypeArray(out string genericTypeArgName)
        {
            var currType = TypeDefinition;

            if (IsTypeArray(TypeDefinition, out genericTypeArgName))
            {
                return true;
            }

            foreach (var i in TypeDefinition.GetInterfaces())
            {
                if (IsTypeArray(i, out genericTypeArgName))
                {
                    return true;
                }
            }
            genericTypeArgName = "";
            return false;
        }

        private bool IsTypeArray(Type typeReference, out string genericTypeArgName)
        {
            if (typeReference.FullName.StartsWith("Windows.Foundation.Collections.IVector`1") ||
                typeReference.FullName.StartsWith("Windows.Foundation.Collections.IVectorView`1")
                )
            {
                var genericInstanceType = typeReference; //as GenericInstanceType;
                if (genericInstanceType == null)
                {
                    genericTypeArgName = "T";
                }
                else
                {
                    genericTypeArgName = genericInstanceType.GetGenericArguments()[0].ToTypeScriptTypeName();
                }
                return true;
            }

            genericTypeArgName = "";
            return false;
        }

        #endregion


    }
    
}
