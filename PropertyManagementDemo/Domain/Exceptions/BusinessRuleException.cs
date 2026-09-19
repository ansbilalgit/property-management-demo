namespace Domain.Exceptions
{
    public class BusinessRuleException : Exception
    {
        public BusinessRuleException(string key, string message) : base(message) => Key = key;

        public string Key { get; }
    }
}
