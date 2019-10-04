namespace UnitTests.Model
{
    public enum Gender { Male, Female };

    public enum Term { Fall, Spring };

    static public class EnumValues
    {
        static public readonly (Gender g, string c)[] GenderValues =
                new[] {
                    (Gender.Male, "M"),
                    (Gender.Female, "F")
                };

        static public readonly (Term t, string c)[] TermValues =
                new[] {
                    (Term.Fall, "F"),
                    (Term.Spring, "S")
                };
    }
}