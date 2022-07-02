using dnlib;
using dnlib.DotNet;
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
    public enum DifferenceType
    {
        Identical,
        Created,
        Removed,
        Modified
    }
    public enum SubjectType
    {
        None,
        RuntimeVersion,
        AssemblyReference,
        Type,
        Field,
        Property,
        Attribute,
        AttributeValue,
        Method
    }
    public class Difference
    {
        public DifferenceType Type { get; set; }
        public SubjectType Subject { get; set; }
        public string OldValue { get; set; }
        public string NewValue { get; set; }

        public Difference(DifferenceType type = default, SubjectType subject = default, string oldValue = default, string newValue = default)
        {
            Type = type;
            Subject = subject;
            OldValue = oldValue;
            NewValue = newValue;
        }
    }
    public class Difference<T> : Difference
    {
        public T OldObject { get; set; }
        public T NewObject { get; set; }

        public Difference(T oldObject = default, T newObject = default, DifferenceType type = default, SubjectType subject = default, string oldValue = default, string newValue = default)
            : base(type, subject, oldValue, newValue)
        {
            OldObject = oldObject;
            NewObject = newObject;
        }
    }

    public class Comparer
    {
        private readonly List<Difference> _differences;

        public Comparer()
        {
            _differences = new List<Difference>();
        }

        public IEnumerable<Difference> Compare(Stream oldAssembly, Stream newAssembly)
        {
            var oldModule = ModuleDefMD.Load(oldAssembly);
            var newModule = ModuleDefMD.Load(newAssembly);
            CompareModules(oldModule, newModule);

            return _differences.ToList();
        }

        private void CompareModules(ModuleDefMD oldModule, ModuleDefMD newModule)
        {
            CheckModule();
            CheckModuleAssemblyReferences();
            CheckTypes();

            void CheckTypes()
            {
                var oldTypes = oldModule.Types.ToList();
                var newTypes = newModule.Types.ToList();

                var createdTypes = newTypes.ExceptBy(oldTypes.Select(_ => _.Name), _ => _.Name).ToList();
                var removedTypes = oldTypes.ExceptBy(newTypes.Select(_ => _.Name), _ => _.Name).ToList();

                var commonNewTypes = newTypes.IntersectBy(oldTypes.Select(_ => _.Name), _ => _.Name).ToList();
                var commonOldTypes = oldTypes.IntersectBy(newTypes.Select(_ => _.Name), _ => _.Name).ToList();

                foreach (var removedType in removedTypes)
                {
                    _differences.Add(new Difference<TypeDef>(
                        removedType,
                        null,
                        DifferenceType.Removed,
                        SubjectType.Type,
                        removedType.FullName.ToString(),
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
                        createdType.FullName.ToString()
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
                var oldReferences = oldModule.GetAssemblyRefs().ToList();
                var newReferences = newModule.GetAssemblyRefs().ToList();

                var createdReferences = newReferences.ExceptBy(oldReferences.Select(_ => _.FullName), _ => _.FullName).ToList();
                var removedReferences = oldReferences.ExceptBy(newReferences.Select(_ => _.FullName), _ => _.FullName).ToList();

                var commonNewReferences = newReferences.IntersectBy(oldReferences.Select(_ => _.FullName), _ => _.FullName).ToList();
                var commonOldReferences = oldReferences.IntersectBy(newReferences.Select(_ => _.FullName), _ => _.FullName).ToList();

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

            // Add Attribute comparer

            void CompareMethods()
            {
                var oldMethods = oldType.Methods.ToList();
                var newMethods = newType.Methods.ToList();

                var createdMethods = newMethods.ExceptBy(oldMethods.Select(_ => _.FullName), _ => _.FullName).ToList();
                var removedMethods = oldMethods.ExceptBy(newMethods.Select(_ => _.FullName), _ => _.FullName).ToList();

                var commonNewMethods = newMethods.IntersectBy(oldMethods.Select(_ => _.FullName), _ => _.FullName).ToList();
                var commonOldMethods = oldMethods.IntersectBy(newMethods.Select(_ => _.FullName), _ => _.FullName).ToList();

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
                var oldFields = oldType.Fields.ToList();
                var newFields = newType.Fields.ToList();

                var createdFields = newFields.ExceptBy(oldFields.Select(_ => _.Name), _ => _.Name).ToList();
                var removedFields = oldFields.ExceptBy(newFields.Select(_ => _.Name), _ => _.Name).ToList();

                var commonNewFields = newFields.IntersectBy(oldFields.Select(_ => _.Name), _ => _.Name).ToList();
                var commonOldFields = oldFields.IntersectBy(newFields.Select(_ => _.Name), _ => _.Name).ToList();

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
                var oldAttributes = oldType.Attributes.ToString();
                var newAttributes = newType.Attributes.ToString();
                if (oldAttributes != newAttributes)
                {
                    _differences.Add(new Difference<TypeAttributes>(
                        oldType.Attributes,
                        newType.Attributes,
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
                var oldAttributes = oldProperty.CustomAttributes.ToList();
                var newAttributes = oldProperty.CustomAttributes.ToList();

                var createdAttributes = newAttributes.ExceptBy(oldAttributes.Select(_ => _.AttributeType.Name), _ => _.AttributeType.Name).ToList();
                var removedAttributes = oldAttributes.ExceptBy(newAttributes.Select(_ => _.AttributeType.Name), _ => _.AttributeType.Name).ToList();

                var commonNewAttributes = newAttributes.IntersectBy(oldAttributes.Select(_ => _.AttributeType.Name), _ => _.AttributeType.Name).ToList();
                var commonOldAttributes = oldAttributes.IntersectBy(newAttributes.Select(_ => _.AttributeType.Name), _ => _.AttributeType.Name).ToList();


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

                    var oldFields = attributePair.Item1.Fields.Union(attributePair.Item1.Properties).ToList();
                    var newFields = attributePair.Item2.Fields.Union(attributePair.Item2.Properties).ToList();

                    var createdFields = newFields.ExceptBy(oldFields.Select(_ => _.Name), _ => _.Name).ToList();
                    var removedFields = oldFields.ExceptBy(newFields.Select(_ => _.Name), _ => _.Name).ToList();

                    var commonNewFields = newFields.IntersectBy(oldFields.Select(_ => _.Name), _ => _.Name).ToList();
                    var commonOldFields = oldFields.IntersectBy(newFields.Select(_ => _.Name), _ => _.Name).ToList();

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
                var oldAttributes = oldField.CustomAttributes.ToList();
                var newAttributes = oldField.CustomAttributes.ToList();

                var createdAttributes = newAttributes.ExceptBy(oldAttributes.Select(_ => _.AttributeType.Name), _ => _.AttributeType.Name).ToList();
                var removedAttributes = oldAttributes.ExceptBy(newAttributes.Select(_ => _.AttributeType.Name), _ => _.AttributeType.Name).ToList();

                var commonNewAttributes = newAttributes.IntersectBy(oldAttributes.Select(_ => _.AttributeType.Name), _ => _.AttributeType.Name).ToList();
                var commonOldAttributes = oldAttributes.IntersectBy(newAttributes.Select(_ => _.AttributeType.Name), _ => _.AttributeType.Name).ToList();


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

                    var oldFields = attributePair.Item1.Fields.Union(attributePair.Item1.Properties).ToList();
                    var newFields = attributePair.Item2.Fields.Union(attributePair.Item2.Properties).ToList();

                    var createdFields = newFields.ExceptBy(oldFields.Select(_ => _.Name), _ => _.Name).ToList();
                    var removedFields = oldFields.ExceptBy(newFields.Select(_ => _.Name), _ => _.Name).ToList();

                    var commonNewFields = newFields.IntersectBy(oldFields.Select(_ => _.Name), _ => _.Name).ToList();
                    var commonOldFields = oldFields.IntersectBy(newFields.Select(_ => _.Name), _ => _.Name).ToList();

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
        private void CompareTwoMethods(MethodDef newField, MethodDef newMethod)
        {

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
                string paramString = String.Join(",", method.Parameters.Select(_ => $"{_.Type.TypeName} {_.Name}"));
                builder.Append(paramString);
            }
            builder.Append(")");

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
