// Stand-ins for "the game" and for mod code that hooks it. The hook checker is pointed at this
// very assembly, so every class here is read back through Cecil and checked against Target.
using HarmonyLib;

#pragma warning disable CS0414 // fixture fields exist to be looked up, not read

namespace ObenCore.Tests.Fixtures;

public class Target
{
    public int Count;
    private string name = "";

    public int Value { get; set; }
    public int ReadOnly => 1;

    public void Hit(int amount, string who) { }
    public void Over(int a) { }
    public void Over(string a) { }
    public void Plain() { }
}

public class Derived : Target
{
}

[HarmonyPatch(typeof(Target), "Hit")]
static class GoodPatch
{
    static void Prefix(int amount, string who, Target __instance, int ___Count, string ___name) { }
}

[HarmonyPatch(typeof(Target), "Gone")]
static class MissingMethodPatch
{
    static void Prefix() { }
}

[HarmonyPatch(typeof(Target), "Over")]
static class AmbiguousPatch
{
    static void Prefix() { }
}

[HarmonyPatch(typeof(Target), "Over", new[] { typeof(int) })]
static class OverloadPatch
{
    static void Prefix(int a) { }
}

[HarmonyPatch(typeof(Target), "Over", new[] { typeof(float) })]
static class WrongOverloadPatch
{
    static void Prefix() { }
}

[HarmonyPatch(typeof(Derived), "Plain")]
static class InheritedPatch
{
    static void Prefix() { }
}

[HarmonyPatch(typeof(Target), "Hit")]
static class RenamedParameterPatch
{
    static void Postfix(int amout, string who, int ___missing) { }
}

[HarmonyPatch(typeof(Target))]
static class SplitAcrossClassAndMethod
{
    [HarmonyPrefix]
    [HarmonyPatch("Hit")]
    static void First(int amount) { }

    [HarmonyPrefix]
    [HarmonyPatch("Nowhere")]
    static void Second() { }
}

[HarmonyPatch(typeof(Target), "Value", MethodType.Getter)]
static class GetterPatch
{
    static void Postfix(ref int __result) { }
}

[HarmonyPatch(typeof(Target), "ReadOnly", MethodType.Setter)]
static class MissingSetterPatch
{
    static void Prefix() { }
}

[HarmonyPatch(typeof(Target), MethodType.Constructor)]
static class ConstructorPatch
{
    static void Prefix() { }
}

[HarmonyPatch("ObenCore.Tests.Fixtures.GoneType", "Run")]
static class GoneTypePatch
{
    static void Prefix() { }
}

[HarmonyPatch(typeof(Target))]
static class NoMethodNamed
{
    static void Prefix() { }
}

[HarmonyPatch]
static class ComputedTarget
{
    static System.Reflection.MethodBase TargetMethod() => null;

    static void Prefix() { }
}

[HarmonyPatch(typeof(Target), "Plain")]
[HarmonyPatchCategory("fixture-category")]
static class CategoryPatch
{
    static void Prefix() { }
}

static class Lookups
{
    static void Run(string dynamicName)
    {
        AccessTools.Method(typeof(Target), "Hit");
        AccessTools.Method(typeof(Target), "Gone");
        AccessTools.Method(typeof(Target), "Over");
        AccessTools.Method(typeof(Target), "Over", new[] { typeof(string) });
        AccessTools.Method(typeof(Target), "Over", new[] { typeof(double) });
        AccessTools.Method(typeof(Derived), "Plain");
        AccessTools.DeclaredMethod(typeof(Derived), "Plain");
        AccessTools.Field(typeof(Target), "name");
        AccessTools.Field(typeof(Target), "nome");
        AccessTools.DeclaredField(typeof(Derived), "Count");
        AccessTools.FieldRefAccess<Target, int>("Count");
        AccessTools.FieldRefAccess<Target, int>("Cnt");
        AccessTools.Property(typeof(Target), "Value");
        AccessTools.PropertySetter(typeof(Target), "ReadOnly");
        AccessTools.Method("ObenCore.Tests.Fixtures.Target:Plain");
        AccessTools.Method("ObenCore.Tests.Fixtures.Target:Absent");
        AccessTools.Method(typeof(Target), dynamicName);
    }
}
