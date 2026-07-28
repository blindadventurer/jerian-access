using System;
using System.Reflection;
using System.Text;

namespace JerianAccess.Module.Game
{
    /// <summary>
    /// Every screen reaches into the game through static FieldInfo / MethodInfo lookups by name.
    /// A name the game has since renamed resolves to NULL and stays silent until the surface it
    /// belongs to appears - and then it is a crash inside an announcement, on a screen the player
    /// cannot see, possibly hours into a run (the year-increment flash read nothing for exactly
    /// this reason: three field names carried over from a build that renamed them).
    ///
    /// So the module audits ITSELF at load: it walks its own types for static reflection handles,
    /// forces them to resolve, and logs every one that came back null. No table to maintain - a
    /// screen written tomorrow is covered by declaring its lookups the way every other screen
    /// does.
    /// </summary>
    internal static class ReflectionAudit
    {
        public static void Run()
        {
            var missing = new StringBuilder();
            int checked_ = 0;
            foreach (var type in typeof(ReflectionAudit).Assembly.GetTypes())
            {
                foreach (var field in type.GetFields(
                    BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public))
                {
                    if (!typeof(MemberInfo).IsAssignableFrom(field.FieldType)) continue;
                    checked_++;
                    object handle;
                    try
                    {
                        handle = field.GetValue(null);
                    }
                    catch (Exception e)
                    {
                        // A static initializer that throws takes its whole screen with it.
                        missing.Append(type.Name).Append('.').Append(field.Name)
                            .Append(" threw ").Append(e.GetBaseException().Message).Append("; ");
                        continue;
                    }
                    if (handle == null)
                        missing.Append(type.Name).Append('.').Append(field.Name).Append("; ");
                }
            }
            if (missing.Length > 0)
                Mod.Error("reflection audit: game members these lookups name are GONE - the "
                    + "screens using them will fail where they are used: " + missing);
            else
                Mod.Log("reflection audit: " + checked_ + " game member lookups resolve");
        }
    }
}
