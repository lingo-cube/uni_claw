// Polyfill for IsExternalInit — required for record class on netstandard2.0
namespace System.Runtime.CompilerServices
{
    internal static class IsExternalInit { }
}
