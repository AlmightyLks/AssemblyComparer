namespace AssemblyComparer.Core
{
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
}
