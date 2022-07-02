using dnlib;
using dnlib.DotNet;
using dnlib.DotNet.Emit;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime;
using System.Security.Cryptography;
using System.Text;

namespace AssemblyComparer.Core
{
    public class Comparator
    {
        private readonly List<Difference> _differences;

        public Comparator()
        {
            _differences = new List<Difference>();
        }

        public Difference[] Compare(Stream oldAssembly, Stream newAssembly)
        {
            var oldModule = ModuleDefMD.Load(oldAssembly);
            var newModule = ModuleDefMD.Load(newAssembly);
            CompareModules(oldModule, newModule);

            var result = _differences.ToArray();
            _differences.Clear();
            return result;
        }

        private void CompareModules(ModuleDefMD oldModule, ModuleDefMD newModule)
        {
            CheckModule();
            CheckModuleAssemblyReferences();
            CheckTypes();

            void CheckTypes()
            {
                var oldTypes = oldModule.Types;
                var newTypes = newModule.Types;

                var createdTypes = newTypes.ExceptBy(oldTypes.Select(_ => _.Name), _ => _.Name);
                var removedTypes = oldTypes.ExceptBy(newTypes.Select(_ => _.Name), _ => _.Name);

                var commonNewTypes = newTypes.IntersectBy(oldTypes.Select(_ => _.Name), _ => _.Name);
                var commonOldTypes = oldTypes.IntersectBy(newTypes.Select(_ => _.Name), _ => _.Name);

                foreach (var removedType in removedTypes)
                {
                    _differences.Add(new Difference<TypeDef>(
                        removedType,
                        null,
                        DifferenceType.Removed,
                        SubjectType.Type,
                        BuildTypeDef(removedType),
                        null
                        ));
                }
                foreach (var createdType in createdTypes)
                {
                    _differences.Add(new Difference<TypeDef>(
                        null,
                        createdType,
                        DifferenceType.Created,
                        SubjectType.Type,
                        null,
                        BuildTypeDef(createdType)
                        ));
                }

                // Check common references for modified versions
                foreach (var typePair in commonNewTypes.Zip(commonOldTypes, Tuple.Create))
                {
                    // Item1 new
                    // Item2 old
                    CompareTwoTypes(typePair.Item2, typePair.Item1);
                }
            }
            void CheckModuleAssemblyReferences()
            {
                var oldReferences = oldModule.GetAssemblyRefs();
                var newReferences = newModule.GetAssemblyRefs();

                var createdReferences = newReferences.ExceptBy(oldReferences.Select(_ => _.FullName), _ => _.FullName);
                var removedReferences = oldReferences.ExceptBy(newReferences.Select(_ => _.FullName), _ => _.FullName);

                var commonNewReferences = newReferences.IntersectBy(oldReferences.Select(_ => _.FullName), _ => _.FullName);
                var commonOldReferences = oldReferences.IntersectBy(newReferences.Select(_ => _.FullName), _ => _.FullName);

                foreach (var removedReference in removedReferences)
                {
                    _differences.Add(new Difference<AssemblyRef>(
                        removedReference,
                        null,
                        DifferenceType.Removed,
                        SubjectType.AssemblyReference,
                        removedReference.FullName.ToString(),
                        null
                        ));
                }
                foreach (var createdReference in createdReferences)
                {
                    _differences.Add(new Difference<AssemblyRef>(
                        null,
                        createdReference,
                        DifferenceType.Created,
                        SubjectType.AssemblyReference,
                        null,
                        createdReference.FullName.ToString()
                        ));
                }

                // Check common references for modified versions
                foreach (var referencePair in commonNewReferences.Zip(commonOldReferences, Tuple.Create))
                {
                    // Item1 new
                    // Item2 old

                    if (referencePair.Item1.Version != referencePair.Item2.Version)
                    {
                        _differences.Add(new Difference<AssemblyRef>(
                            referencePair.Item2,
                            referencePair.Item1,
                            DifferenceType.Modified,
                            SubjectType.AssemblyReference,
                            referencePair.Item1.Version.ToString(),
                            referencePair.Item2.Version.ToString()
                            ));
                    }
                }
            }
            void CheckModule()
            {
                if (oldModule.Name.String != newModule.Name.String)
                {
                    _differences.Add(new Difference<ModuleDef>(
                        oldModule,
                        newModule,
                        DifferenceType.Modified,
                        SubjectType.AssemblyName,
                        oldModule.Name.String,
                        newModule.Name.String
                        ));
                }
                if (oldModule.RuntimeVersion != newModule.RuntimeVersion)
                {
                    _differences.Add(new Difference<ModuleDef>(
                        oldModule,
                        newModule,
                        DifferenceType.Modified,
                        SubjectType.RuntimeVersion,
                        oldModule.RuntimeVersion.ToString(),
                        newModule.RuntimeVersion.ToString()
                        ));
                }

            }
        }
        private void CompareTwoTypes(TypeDef oldType, TypeDef newType)
        {
            // Modifiers
            CompareModifiers();

            // Fields
            CompareFields();

            // Methods
            CompareMethods();

            // Properties
            CompareProperties();

            // Add Attribute comparer

            void CompareProperties()
            {
                var oldProperties = oldType.Properties;
                var newProperties = newType.Properties;

                // PropertyDef#FullName includes whole method definition, so it will sort out parameter changes and alike already
                var createdProperties = newProperties.ExceptBy(oldProperties.Select(_ => _.FullName), _ => _.FullName);
                var removedProperties = oldProperties.ExceptBy(newProperties.Select(_ => _.FullName), _ => _.FullName);

                var commonNewProperties = newProperties.IntersectBy(oldProperties.Select(_ => _.FullName), _ => _.FullName);
                var commonOldProperties = oldProperties.IntersectBy(newProperties.Select(_ => _.FullName), _ => _.FullName);

                foreach (var newProperty in createdProperties)
                {
                    _differences.Add(new Difference<PropertyDef>(
                        null,
                        newProperty,
                        DifferenceType.Created,
                        SubjectType.Method,
                        null,
                        newProperty.FullName
                        )); 
                }
                foreach (var removedMethod in removedProperties)
                {
                    _differences.Add(new Difference<PropertyDef>(
                        removedMethod,
                        null,
                        DifferenceType.Removed,
                        SubjectType.Method,
                        removedMethod.FullName,
                        null
                        ));
                }

                // Check common fields for modified versions
                foreach (var fieldPair in commonNewProperties.Zip(commonOldProperties, Tuple.Create))
                {
                    // Item1 new
                    // Item2 old
                    CompareTwoProperties(fieldPair.Item2, fieldPair.Item1);
                }
            }
            void CompareMethods()
            {
                var oldMethods = oldType.Methods;
                var newMethods = newType.Methods;

                // MethodDef#FullName includes whole method definition, so it will sort out parameter changes and alike already
                var createdMethods = newMethods.ExceptBy(oldMethods.Select(_ => _.FullName), _ => _.FullName);
                var removedMethods = oldMethods.ExceptBy(newMethods.Select(_ => _.FullName), _ => _.FullName);

                var commonNewMethods = newMethods.IntersectBy(oldMethods.Select(_ => _.FullName), _ => _.FullName);
                var commonOldMethods = oldMethods.IntersectBy(newMethods.Select(_ => _.FullName), _ => _.FullName);

                foreach (var newMethod in createdMethods)
                {
                    _differences.Add(new Difference<MethodDef>(
                        null,
                        newMethod,
                        DifferenceType.Created,
                        SubjectType.Method,
                        null,
                        BuildMethodDef(newMethod)
                        ));
                }
                foreach (var removedMethod in removedMethods)
                {
                    _differences.Add(new Difference<MethodDef>(
                        removedMethod,
                        null,
                        DifferenceType.Removed,
                        SubjectType.Method,
                        BuildMethodDef(removedMethod),
                        null
                        ));
                }

                // Check common fields for modified versions
                foreach (var fieldPair in commonNewMethods.Zip(commonOldMethods, Tuple.Create))
                {
                    // Item1 new
                    // Item2 old
                    CompareTwoMethods(fieldPair.Item2, fieldPair.Item1);
                }
            }
            void CompareFields()
            {
                var oldFields = oldType.Fields;
                var newFields = newType.Fields;

                var createdFields = newFields.ExceptBy(oldFields.Select(_ => _.Name), _ => _.Name);
                var removedFields = oldFields.ExceptBy(newFields.Select(_ => _.Name), _ => _.Name);

                var commonNewFields = newFields.IntersectBy(oldFields.Select(_ => _.Name), _ => _.Name);
                var commonOldFields = oldFields.IntersectBy(newFields.Select(_ => _.Name), _ => _.Name);

                foreach (var newField in createdFields)
                {
                    _differences.Add(new Difference<FieldDef>(
                        null,
                        newField,
                        DifferenceType.Created,
                        SubjectType.Field,
                        null,
                        BuildFieldDef(newField)
                        ));
                }
                foreach (var removedField in removedFields)
                {
                    _differences.Add(new Difference<FieldDef>(
                        removedField,
                        null,
                        DifferenceType.Removed,
                        SubjectType.Field,
                        BuildFieldDef(removedField),
                        null
                        ));
                }

                // Check common fields for modified versions
                foreach (var fieldPair in commonNewFields.Zip(commonOldFields, Tuple.Create))
                {
                    // Item1 new
                    // Item2 old
                    CompareTwoFields(fieldPair.Item2, fieldPair.Item1);
                }
            }
            void CompareModifiers()
            {
                var oldAttributes = BuildTypeDef(oldType);
                var newAttributes = BuildTypeDef(newType);
                if (oldAttributes != newAttributes)
                {
                    _differences.Add(new Difference<TypeDef>(
                        oldType,
                        newType,
                        DifferenceType.Modified,
                        SubjectType.Type,
                        oldAttributes,
                        newAttributes
                        ));
                }
            }
        }

        private void CompareTwoProperties(PropertyDef oldProperty, PropertyDef newProperty)
        {
            // TODO:
            // Differentiate property diffs



            /*
            Difference<PropertyDef> diff = null;

            bool oldHasGetter = oldProperty.GetMethod != null;
            bool newHasGetter = newProperty.GetMethod != null;

            bool oldHasSetter = oldProperty.SetMethod != null;
            bool newHasSetter = newProperty.SetMethod != null;

            if (oldProperty.GetMethod.IsStatic != newProperty.GetMethod.IsStatic)
            {
                _differences.Add(new Difference<PropertyDef>(
                    oldProperty,
                    newProperty,
                    DifferenceType.Modified,
                    Difference.FieldChanged_Text,
                    nameof(FieldDef.CustomAttributes),
                    oldProperty.GetMethod.IsStatic.ToString(),
                    newProperty.GetMethod.IsStatic.ToString()
                    ));
            }
            if (oldProperty.GetMethod.IsPrivate != newProperty.GetMethod.IsPrivate)
            {
                _differences.Add(new Difference<PropertyDef>(
                    oldProperty,
                    newProperty,
                    DifferenceType.Modified,
                    Difference.FieldChanged_Text,
                    nameof(FieldDef.IsPrivate),
                    oldProperty.GetMethod.IsPrivate.ToString(),
                    newProperty.GetMethod.IsPrivate.ToString()
                    ));
            }

            CompareAttributes();
            */

            void CompareAttributes()
            {
                var oldAttributes = oldProperty.CustomAttributes;
                var newAttributes = oldProperty.CustomAttributes;

                var createdAttributes = newAttributes.ExceptBy(oldAttributes.Select(_ => _.AttributeType.Name), _ => _.AttributeType.Name);
                var removedAttributes = oldAttributes.ExceptBy(newAttributes.Select(_ => _.AttributeType.Name), _ => _.AttributeType.Name);

                var commonNewAttributes = newAttributes.IntersectBy(oldAttributes.Select(_ => _.AttributeType.Name), _ => _.AttributeType.Name);
                var commonOldAttributes = oldAttributes.IntersectBy(newAttributes.Select(_ => _.AttributeType.Name), _ => _.AttributeType.Name);


                foreach (var createdAttribute in createdAttributes)
                {
                    _differences.Add(new Difference<CustomAttribute>(
                        null,
                        createdAttribute,
                        DifferenceType.Created,
                        SubjectType.Field,
                        BuildCustomAttributeDefinition(createdAttribute),
                        null
                        ));
                }
                foreach (var removedAttribute in removedAttributes)
                {
                    _differences.Add(new Difference<CustomAttribute>(
                        removedAttribute,
                        null,
                        DifferenceType.Removed,
                        SubjectType.Field,
                        null,
                        BuildCustomAttributeDefinition(removedAttribute)
                        ));
                }

                foreach (var attributePair in commonNewAttributes.Zip(commonOldAttributes, Tuple.Create))
                {
                    // Item1 new
                    // Item2 old

                    var oldFields = attributePair.Item1.Fields.Union(attributePair.Item1.Properties);
                    var newFields = attributePair.Item2.Fields.Union(attributePair.Item2.Properties);

                    var createdFields = newFields.ExceptBy(oldFields.Select(_ => _.Name), _ => _.Name);
                    var removedFields = oldFields.ExceptBy(newFields.Select(_ => _.Name), _ => _.Name);

                    var commonNewFields = newFields.IntersectBy(oldFields.Select(_ => _.Name), _ => _.Name);
                    var commonOldFields = oldFields.IntersectBy(newFields.Select(_ => _.Name), _ => _.Name);

                    // Check common attributes for value changes
                    foreach (var fieldPair in commonNewFields.Zip(commonOldFields, Tuple.Create))
                    {
                        // Item1 new
                        // Item2 old
                        if (fieldPair.Item1.Argument.Value != fieldPair.Item2.Argument.Value)
                        {
                            _differences.Add(new Difference<CAArgument>(
                                fieldPair.Item2.Argument,
                                fieldPair.Item1.Argument,
                                DifferenceType.Removed,
                                SubjectType.AttributeValue,
                                fieldPair.Item2.Argument.Value.ToString(),
                                fieldPair.Item1.Argument.Value.ToString()
                                ));
                        }
                    }
                }
            }
        }
        private void CompareTwoFields(FieldDef oldField, FieldDef newField)
        {
            if (oldField.IsStatic != newField.IsStatic)
            {
                _differences.Add(new Difference<FieldDef>(
                    oldField,
                    newField,
                    DifferenceType.Modified,
                    SubjectType.Field,
                    BuildFieldDef(oldField),
                    BuildFieldDef(newField)
                    ));
            }
            if (oldField.IsPrivate != newField.IsPrivate)
            {
                _differences.Add(new Difference<FieldDef>(
                    oldField,
                    newField,
                    DifferenceType.Modified,
                    SubjectType.Field,
                    BuildFieldDef(oldField),
                    BuildFieldDef(newField)
                    ));
            }
            if (oldField.IsInitOnly != newField.IsInitOnly)
            {
                _differences.Add(new Difference<FieldDef>(
                    oldField,
                    newField,
                    DifferenceType.Modified,
                    SubjectType.Field,
                    BuildFieldDef(oldField),
                    BuildFieldDef(newField)
                    ));
            }
            if (oldField.FieldType.FullName != newField.FieldType.FullName)
            {
                _differences.Add(new Difference<FieldDef>(
                    oldField,
                    newField,
                    DifferenceType.Modified,
                    SubjectType.Field,
                    BuildFieldDef(oldField),
                    BuildFieldDef(newField)
                    ));
            }

            CompareAttributes();

            void CompareAttributes()
            {
                var oldAttributes = oldField.CustomAttributes;
                var newAttributes = oldField.CustomAttributes;

                var createdAttributes = newAttributes.ExceptBy(oldAttributes.Select(_ => _.AttributeType.Name), _ => _.AttributeType.Name);
                var removedAttributes = oldAttributes.ExceptBy(newAttributes.Select(_ => _.AttributeType.Name), _ => _.AttributeType.Name);

                var commonNewAttributes = newAttributes.IntersectBy(oldAttributes.Select(_ => _.AttributeType.Name), _ => _.AttributeType.Name);
                var commonOldAttributes = oldAttributes.IntersectBy(newAttributes.Select(_ => _.AttributeType.Name), _ => _.AttributeType.Name);


                foreach (var createdAttribute in createdAttributes)
                {
                    _differences.Add(new Difference<CustomAttribute>(
                        null,
                        createdAttribute,
                        DifferenceType.Created,
                        SubjectType.Field,
                        BuildCustomAttributeDefinition(createdAttribute),
                        null
                        ));
                }
                foreach (var removedAttribute in removedAttributes)
                {
                    _differences.Add(new Difference<CustomAttribute>(
                        removedAttribute,
                        null,
                        DifferenceType.Removed,
                        SubjectType.Attribute,
                        null,
                        BuildCustomAttributeDefinition(removedAttribute)
                        ));
                }

                foreach (var attributePair in commonNewAttributes.Zip(commonOldAttributes, Tuple.Create))
                {
                    // Item1 new
                    // Item2 old

                    var oldFields = attributePair.Item1.Fields.Union(attributePair.Item1.Properties);
                    var newFields = attributePair.Item2.Fields.Union(attributePair.Item2.Properties);

                    var createdFields = newFields.ExceptBy(oldFields.Select(_ => _.Name), _ => _.Name);
                    var removedFields = oldFields.ExceptBy(newFields.Select(_ => _.Name), _ => _.Name);

                    var commonNewFields = newFields.IntersectBy(oldFields.Select(_ => _.Name), _ => _.Name);
                    var commonOldFields = oldFields.IntersectBy(newFields.Select(_ => _.Name), _ => _.Name);

                    // Check common attributes for value changes
                    foreach (var fieldPair in commonNewFields.Zip(commonOldFields, Tuple.Create))
                    {
                        // Item1 new
                        // Item2 old
                        if (fieldPair.Item1.Argument.Value != fieldPair.Item2.Argument.Value)
                        {
                            _differences.Add(new Difference<CAArgument>(
                                fieldPair.Item2.Argument,
                                fieldPair.Item1.Argument,
                                DifferenceType.Removed,
                                SubjectType.AttributeValue,
                                fieldPair.Item2.Argument.Value.ToString(),
                                fieldPair.Item1.Argument.Value.ToString()
                                ));
                        }
                    }
                }
            }
        }
        private void CompareTwoMethods(MethodDef oldMethod, MethodDef newMethod)
        {
            // Technically those aren't going to trigger, because the sorting out by MethodDef#FullName
            // should already have handled that
            if (oldMethod.IsStatic != newMethod.IsStatic)
            {
                _differences.Add(new Difference<MethodDef>(
                    oldMethod,
                    newMethod,
                    DifferenceType.Modified,
                    SubjectType.Method,
                    BuildMethodDef(oldMethod),
                    BuildMethodDef(newMethod)
                    ));
            }
            if (oldMethod.IsAbstract != newMethod.IsAbstract)
            {
                _differences.Add(new Difference<MethodDef>(
                    oldMethod,
                    newMethod,
                    DifferenceType.Modified,
                    SubjectType.Method,
                    BuildMethodDef(oldMethod),
                    BuildMethodDef(newMethod)
                    ));
            }
            if (oldMethod.IsFinal != newMethod.IsFinal)
            {
                _differences.Add(new Difference<MethodDef>(
                    oldMethod,
                    newMethod,
                    DifferenceType.Modified,
                    SubjectType.Method,
                    BuildMethodDef(oldMethod),
                    BuildMethodDef(newMethod)
                    ));
            }
            if (oldMethod.IsVirtual != newMethod.IsVirtual)
            {
                _differences.Add(new Difference<MethodDef>(
                    oldMethod,
                    newMethod,
                    DifferenceType.Modified,
                    SubjectType.Method,
                    BuildMethodDef(oldMethod),
                    BuildMethodDef(newMethod)
                    ));
            }
            if (oldMethod.Parameters.Count != newMethod.Parameters.Count)
            {
                _differences.Add(new Difference<MethodDef>(
                    oldMethod,
                    newMethod,
                    DifferenceType.Modified,
                    SubjectType.Method,
                    BuildMethodDef(oldMethod),
                    BuildMethodDef(newMethod)
                    ));
            }
            if (oldMethod.GenericParameters.Count != newMethod.GenericParameters.Count)
            {
                _differences.Add(new Difference<MethodDef>(
                    oldMethod,
                    newMethod,
                    DifferenceType.Modified,
                    SubjectType.Method,
                    BuildMethodDef(oldMethod),
                    BuildMethodDef(newMethod)
                    ));
            }

            // Differentiate between method bodies
            if (oldMethod.HasBody != newMethod.HasBody)
            {
                _differences.Add(new Difference<CilBody>(
                    oldMethod.Body,
                    newMethod.Body,
                    DifferenceType.Modified,
                    SubjectType.Method,
                    $"{oldMethod.Body?.Instructions.Count ?? 0} instructions",
                    $"{newMethod.Body?.Instructions.Count ?? 0} instructions"
                    ));
            }
            else if (oldMethod.Body.Instructions.Count != newMethod.Body.Instructions.Count)
            {
                _differences.Add(new Difference<CilBody>(
                    oldMethod.Body,
                    oldMethod.Body,
                    DifferenceType.Modified,
                    SubjectType.Method,
                    $"{oldMethod.Body?.Instructions.Count ?? 0} instructions",
                    $"{newMethod.Body?.Instructions.Count ?? 0} instructions"
                    ));
            }
            else if (!oldMethod.Body.Instructions.Select(_ => _.ToString()).SequenceEqual(newMethod.Body.Instructions.Select(_ => _.ToString())))
            {
                _differences.Add(new Difference<CilBody>(
                    oldMethod.Body,
                    oldMethod.Body,
                    DifferenceType.Modified,
                    SubjectType.Method,
                    $"{oldMethod.Body?.Instructions.Count ?? 0} instructions",
                    $"{newMethod.Body?.Instructions.Count ?? 0} instructions"
                    ));
            }
        }

        private static string BuildCustomAttributeDefinition(CustomAttribute attribute)
        {
            StringBuilder builder = new StringBuilder();
            var paramStrings = attribute.Fields.Select(_ => $"{_.Name}:{_.Argument.Value}").Union(attribute.Properties.Select(_ => $"{_.Name}:{_.Argument.Value}"));

            builder.Append($"[{attribute.AttributeType.Name}({String.Join(", ", paramStrings)})]");

            return builder.ToString();
        }
        private static string BuildFieldDef(FieldDef field)
        {
            StringBuilder builder = new StringBuilder();

            if (field.IsPublic)
                builder.Append("public ");
            if (field.IsPrivate)
                builder.Append("private ");

            if (field.IsStatic)
                builder.Append("static ");
            if (field.IsInitOnly)
                builder.Append("readonly ");

            builder.Append(field.FieldType.TypeName + " ");
            builder.Append(field.Name);
            builder.Append(";");

            return builder.ToString();
        }
        private static string BuildMethodDef(MethodDef method)
        {
            StringBuilder builder = new StringBuilder();

            if (method.IsPublic)
                builder.Append("public ");
            if (method.IsPrivate)
                builder.Append("private ");

            if (method.IsVirtual)
                builder.Append("virtual ");

            // As the ECMA specs define, "final" is an IL-modifier equal to C#'s "sealed"
            // https://www.ecma-international.org/wp-content/uploads/ECMA-335_6th_edition_june_2012.pdf
            // "To prevent overriding a virtual method use final (see §I.8.10.2) rather than relying on limited accessibility"
            if (method.IsFinal)
                builder.Append("sealed ");

            if (method.IsAbstract)
                builder.Append("abstract ");

            if (method.IsStatic)
                builder.Append("static ");

            builder.Append(method.ReturnType.TypeName + " ");
            builder.Append(method.Name);
            if (method.HasGenericParameters)
            {
                string genericParamString = String.Join(",", method.GenericParameters.Select(_ => _.Name));
                builder.Append($"<{genericParamString}>");
            }
            builder.Append("(");
            if (method.Parameters.Count > 0)
            {
                string paramString = String.Join(",",
                    method.Parameters.Where(_ => !String.IsNullOrWhiteSpace(_.Name)) // "this" is passed with an empty name, sort it out.
                    .Select(_ => $"{_.Type.TypeName} {_.Name}"));
                builder.Append(paramString);
            }
            builder.Append(")");

            return builder.ToString();
        }
        private static string BuildTypeDef(TypeDef type)
        {
            StringBuilder builder = new StringBuilder();

            if (type.IsPublic)
                builder.Append("public ");
            if (type.IsNotPublic) // Likely private
                builder.Append("private ");

            // There is no IsStatic, but the CIL ECMA defines:
            // https://www.ecma-international.org/wp-content/uploads/ECMA-335_6th_edition_june_2012.pdf
            // "A type that is both abstract and sealed should have only static members,
            // and serves as what some languages call a “namespace” or “static class”. end rationale"
            // 
            // There is however no such case, that a user could intentionally create an abstract-sealed class,
            // so we're fine to assume, that if we find one, it is static.
            // https://sharplab.io/#v2:C4LglgNgNAJiDUAfAAgZgAQEMBGBnYATpgMbDq4CmmEFM6yATOgCoAWYBdA3gLABQAXyA===
            if (type.IsAbstract && type.IsSealed)
                builder.Append("static ");
            else if (type.IsAbstract)
                builder.Append("abstract ");
            else if (type.IsSealed)
                builder.Append("sealed ");


            if (type.IsClass)
                builder.Append("class ");
            else if (type.IsEnum)
                builder.Append("enum ");
            else if (type.IsInterface)
                builder.Append("interface ");
            else if (type.IsValueType)
                builder.Append("struct ");


            builder.Append(type.Name);

            if (type.HasGenericParameters)
            {
                string genericParamString = String.Join(",", type.GenericParameters.Select(_ => _.Name));
                builder.Append($"<{genericParamString}>");
            }

            return builder.ToString();
        }

        private object GetInitialValue(FieldDef field)
        {
            switch (field.FieldType.TypeName)
            {
                case nameof(SByte):
                case nameof(Byte):
                    return field.InitialValue.FirstOrDefault();

                case nameof(Int16):
                    return BitConverter.ToInt16(field.InitialValue, 0);
                case nameof(UInt16):
                    return BitConverter.ToUInt16(field.InitialValue, 0);

                case nameof(Int32):
                    return BitConverter.ToInt32(field.InitialValue, 0);
                case nameof(UInt32):
                    return BitConverter.ToUInt32(field.InitialValue, 0);

                case nameof(Int64):
                    return BitConverter.ToInt64(field.InitialValue, 0);
                case nameof(UInt64):
                    return BitConverter.ToUInt64(field.InitialValue, 0);

                case nameof(Single):
                    return BitConverter.ToSingle(field.InitialValue, 0);
                case nameof(Double):
                    return BitConverter.ToDouble(field.InitialValue, 0);
                case nameof(Decimal): //Doesnt have its own converter
                    return BitConverter.ToDouble(field.InitialValue, 0);

                case nameof(Char):
                    return BitConverter.ToChar(field.InitialValue, 0);

                case nameof(String):
                    return Encoding.Default.GetString(field.InitialValue);

                default:
                    return null;
            }
        }
    }
}
