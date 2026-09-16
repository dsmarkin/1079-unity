// Minimal NUnit-compatible shim so the same test sources run here without packages. Unity uses the real NUnit.
using System; using System.Linq; using System.Reflection;
namespace NUnit.Framework {
  [AttributeUsage(AttributeTargets.Method)] public class TestAttribute : Attribute {}
  public class AssertionException : Exception { public AssertionException(string m) : base(m) {} }
  public static class TestContext { public static Ctx CurrentContext = new Ctx(); public class Ctx { public string TestDirectory => AppContext.BaseDirectory; } }
  public static class Assert {
    static void Fail(string m) => throw new AssertionException(m);
    public static void IsTrue(bool c, string m = null) { if (!c) Fail(m ?? "expected true"); }
    public static void IsFalse(bool c, string m = null) { if (c) Fail(m ?? "expected false"); }
    public static void IsNull(object o, string m = null) { if (o != null) Fail(m ?? "expected null"); }
    public static void AreSame(object a, object b) { if (!ReferenceEquals(a, b)) Fail("not same"); }
    public static void AreEqual(object e, object a, string m = null) { if (!Equals(e, a) && !(e is IConvertible && a is IConvertible && Convert.ToDouble(e) == Convert.ToDouble(a))) Fail(m ?? $"expected {e} but was {a}"); }
    public static void AreEqual(double e, double a, double tol, string m = null) { if (Math.Abs(e - a) > tol) Fail(m ?? $"expected {e}±{tol} but was {a}"); }
    public static void Less(double a, double b, string m = null) { if (!(a < b)) Fail(m ?? $"{a} !< {b}"); }
    public static void Greater(double a, double b, string m = null) { if (!(a > b)) Fail(m ?? $"{a} !> {b}"); }
    public static void DoesNotThrow(Action act) { try { act(); } catch (Exception e) { Fail("threw " + e.GetType().Name + ": " + e.Message); } }
    public static void Throws<T>(Action act) where T : Exception { try { act(); } catch (T) { return; } catch (Exception e) { Fail("wrong exception " + e.GetType().Name); } Fail("no exception"); }
  }
  public static class StringAssert { public static void Contains(string needle, string hay) { if (!hay.Contains(needle)) Assert.IsTrue(false, $"'{hay}' lacks '{needle}'"); } }
}
static class Runner {
  static int Main() {
    int pass = 0, fail = 0;
    foreach (var t in Assembly.GetExecutingAssembly().GetTypes().Where(t => t.Namespace == "Height1079.Tests"))
      foreach (var m in t.GetMethods().Where(m => m.GetCustomAttribute<NUnit.Framework.TestAttribute>() != null)) {
        try { m.Invoke(Activator.CreateInstance(t), null); pass++; Console.WriteLine("ok   " + t.Name + "." + m.Name); }
        catch (TargetInvocationException e) { fail++; Console.WriteLine("FAIL " + t.Name + "." + m.Name + ": " + e.InnerException?.Message); }
      }
    Console.WriteLine($"# pass {pass} fail {fail}"); return fail == 0 ? 0 : 1;
  }
}
