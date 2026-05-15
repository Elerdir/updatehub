namespace UpdateHub.Web.Localization;

/// <summary>
/// In-memory translation dictionaries. Keys are dot-separated namespaces
/// (e.g. "nav.dashboard"); missing keys fall back to the key itself so a
/// missing translation is visible without crashing the page.
/// </summary>
public static class UiStrings
{
    public static readonly IReadOnlyList<(string Code, string Label)> Languages =
    [
        ("en", "EN"),
        ("cs", "CS"),
        ("de", "DE"),
    ];

    public static readonly Dictionary<string, Dictionary<string, string>> Resources = new()
    {
        ["en"] = new()
        {
            // Navigation
            ["nav.dashboard"]     = "Dashboard",
            ["nav.applications"]  = "Applications",
            ["nav.settings"]      = "Settings",
            ["nav.security"]      = "Security",
            ["nav.audit"]         = "Audit Log",
            ["nav.logout"]        = "Logout",
            ["nav.theme"]         = "Theme",
            ["nav.language"]      = "Language",
            ["nav.theme.auto"]    = "Auto",
            ["nav.theme.light"]   = "Light",
            ["nav.theme.dark"]    = "Dark",

            // Login
            ["login.title"]       = "Sign in",
            ["login.username"]    = "Username",
            ["login.password"]    = "Password",
            ["login.submit"]      = "Sign in",
            ["login.invalid"]     = "Invalid username or password.",
            ["login.blocked"]     = "Your IP address has been blocked due to too many failed login attempts. Contact the administrator.",
            ["login.totp.title"]  = "Two-factor verification",
            ["login.totp.prompt"] = "Enter the 6-digit code from your authenticator app.",
            ["login.totp.code"]   = "Code",
            ["login.totp.submit"] = "Verify",
            ["login.totp.invalid"]= "Code is invalid or expired.",

            // Dashboard
            ["dashboard.title"]              = "Dashboard",
            ["dashboard.stat.applications"]  = "Applications",
            ["dashboard.stat.published"]     = "Published Releases",
            ["dashboard.stat.downloads"]     = "Total Downloads",
            ["dashboard.applications"]       = "Applications",
            ["dashboard.viewAll"]            = "View all",
            ["dashboard.col.name"]           = "Name",
            ["dashboard.col.slug"]           = "Slug",
            ["dashboard.col.latest"]         = "Latest (Stable)",
            ["dashboard.col.releases"]       = "Releases",
            ["dashboard.empty"]              = "No applications yet.",
            ["dashboard.empty.cta"]          = "Register your first app",
            ["dashboard.manage"]             = "Manage",

            // Common buttons / labels
            ["common.save"]       = "Save",
            ["common.cancel"]     = "Cancel",
            ["common.delete"]     = "Delete",
            ["common.edit"]       = "Edit",
            ["common.create"]     = "Create",
            ["common.publish"]    = "Publish",
            ["common.archive"]    = "Archive",
            ["common.copy"]       = "Copy",
            ["common.copied"]     = "Copied!",
            ["common.confirm"]    = "Confirm",
            ["common.loading"]    = "Loading…",

            // Page titles
            ["page.settings"]     = "Settings",
            ["page.security"]     = "Security",
            ["page.audit"]        = "Audit Log",
            ["page.apps"]         = "Applications",
        },

        ["cs"] = new()
        {
            // Navigation
            ["nav.dashboard"]     = "Přehled",
            ["nav.applications"]  = "Aplikace",
            ["nav.settings"]      = "Nastavení",
            ["nav.security"]      = "Zabezpečení",
            ["nav.audit"]         = "Audit",
            ["nav.logout"]        = "Odhlásit",
            ["nav.theme"]         = "Vzhled",
            ["nav.language"]      = "Jazyk",
            ["nav.theme.auto"]    = "Auto",
            ["nav.theme.light"]   = "Světlý",
            ["nav.theme.dark"]    = "Tmavý",

            // Login
            ["login.title"]       = "Přihlášení",
            ["login.username"]    = "Uživatelské jméno",
            ["login.password"]    = "Heslo",
            ["login.submit"]      = "Přihlásit se",
            ["login.invalid"]     = "Neplatné jméno nebo heslo.",
            ["login.blocked"]     = "Vaše IP adresa byla zablokována kvůli příliš mnoha neúspěšným pokusům o přihlášení. Kontaktujte správce.",
            ["login.totp.title"]  = "Dvoufaktorové ověření",
            ["login.totp.prompt"] = "Zadejte 6místný kód z autentifikační aplikace.",
            ["login.totp.code"]   = "Kód",
            ["login.totp.submit"] = "Ověřit",
            ["login.totp.invalid"]= "Kód je neplatný nebo vypršel.",

            // Dashboard
            ["dashboard.title"]              = "Přehled",
            ["dashboard.stat.applications"]  = "Aplikace",
            ["dashboard.stat.published"]     = "Publikované verze",
            ["dashboard.stat.downloads"]     = "Celkem stažení",
            ["dashboard.applications"]       = "Aplikace",
            ["dashboard.viewAll"]            = "Zobrazit vše",
            ["dashboard.col.name"]           = "Název",
            ["dashboard.col.slug"]           = "Identifikátor",
            ["dashboard.col.latest"]         = "Nejnovější (Stable)",
            ["dashboard.col.releases"]       = "Verze",
            ["dashboard.empty"]              = "Žádné aplikace.",
            ["dashboard.empty.cta"]          = "Zaregistrovat první aplikaci",
            ["dashboard.manage"]             = "Spravovat",

            // Common
            ["common.save"]       = "Uložit",
            ["common.cancel"]     = "Zrušit",
            ["common.delete"]     = "Smazat",
            ["common.edit"]       = "Upravit",
            ["common.create"]     = "Vytvořit",
            ["common.publish"]    = "Publikovat",
            ["common.archive"]    = "Archivovat",
            ["common.copy"]       = "Kopírovat",
            ["common.copied"]     = "Zkopírováno!",
            ["common.confirm"]    = "Potvrdit",
            ["common.loading"]    = "Načítání…",

            // Page titles
            ["page.settings"]     = "Nastavení",
            ["page.security"]     = "Zabezpečení",
            ["page.audit"]        = "Audit log",
            ["page.apps"]         = "Aplikace",
        },

        ["de"] = new()
        {
            // Navigation
            ["nav.dashboard"]     = "Übersicht",
            ["nav.applications"]  = "Anwendungen",
            ["nav.settings"]      = "Einstellungen",
            ["nav.security"]      = "Sicherheit",
            ["nav.audit"]         = "Audit-Log",
            ["nav.logout"]        = "Abmelden",
            ["nav.theme"]         = "Design",
            ["nav.language"]      = "Sprache",
            ["nav.theme.auto"]    = "Auto",
            ["nav.theme.light"]   = "Hell",
            ["nav.theme.dark"]    = "Dunkel",

            // Login
            ["login.title"]       = "Anmelden",
            ["login.username"]    = "Benutzername",
            ["login.password"]    = "Passwort",
            ["login.submit"]      = "Anmelden",
            ["login.invalid"]     = "Ungültiger Benutzername oder Passwort.",
            ["login.blocked"]     = "Ihre IP-Adresse wurde aufgrund zu vieler fehlgeschlagener Anmeldeversuche gesperrt. Wenden Sie sich an den Administrator.",
            ["login.totp.title"]  = "Zwei-Faktor-Verifizierung",
            ["login.totp.prompt"] = "Geben Sie den 6-stelligen Code aus Ihrer Authenticator-App ein.",
            ["login.totp.code"]   = "Code",
            ["login.totp.submit"] = "Bestätigen",
            ["login.totp.invalid"]= "Code ist ungültig oder abgelaufen.",

            // Dashboard
            ["dashboard.title"]              = "Übersicht",
            ["dashboard.stat.applications"]  = "Anwendungen",
            ["dashboard.stat.published"]     = "Veröffentlichte Versionen",
            ["dashboard.stat.downloads"]     = "Downloads gesamt",
            ["dashboard.applications"]       = "Anwendungen",
            ["dashboard.viewAll"]            = "Alle anzeigen",
            ["dashboard.col.name"]           = "Name",
            ["dashboard.col.slug"]           = "Slug",
            ["dashboard.col.latest"]         = "Neueste (Stable)",
            ["dashboard.col.releases"]       = "Versionen",
            ["dashboard.empty"]              = "Noch keine Anwendungen.",
            ["dashboard.empty.cta"]          = "Erste Anwendung registrieren",
            ["dashboard.manage"]             = "Verwalten",

            // Common
            ["common.save"]       = "Speichern",
            ["common.cancel"]     = "Abbrechen",
            ["common.delete"]     = "Löschen",
            ["common.edit"]       = "Bearbeiten",
            ["common.create"]     = "Anlegen",
            ["common.publish"]    = "Veröffentlichen",
            ["common.archive"]    = "Archivieren",
            ["common.copy"]       = "Kopieren",
            ["common.copied"]     = "Kopiert!",
            ["common.confirm"]    = "Bestätigen",
            ["common.loading"]    = "Wird geladen…",

            // Page titles
            ["page.settings"]     = "Einstellungen",
            ["page.security"]     = "Sicherheit",
            ["page.audit"]        = "Audit-Log",
            ["page.apps"]         = "Anwendungen",
        },
    };
}
