namespace CareLanka.Api.Common.Auth;

[AttributeUsage(AttributeTargets.Method, Inherited = false)]
public sealed class AllowWhilePasswordChangeRequiredAttribute : Attribute;
