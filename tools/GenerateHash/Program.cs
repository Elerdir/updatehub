if (args.Length == 0)
{
    Console.WriteLine("Usage:  dotnet run --project tools/GenerateHash -- <password>");
    Console.WriteLine("        dotnet run --project tools/GenerateHash -- mypassword");
    Console.WriteLine();
    Console.WriteLine("Paste the output into UpdateHub:Admin:PasswordHash in appsettings.json");
    Console.WriteLine("or set it as environment variable UpdateHub__Admin__PasswordHash.");
    return 1;
}

var password = args[0];
var hash = BCrypt.Net.BCrypt.HashPassword(password, workFactor: 12);

Console.WriteLine();
Console.WriteLine("Password hash (cost 12):");
Console.WriteLine(hash);
Console.WriteLine();
Console.WriteLine("Verification: " + (BCrypt.Net.BCrypt.Verify(password, hash) ? "OK" : "FAILED"));
return 0;
