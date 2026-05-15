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
            ["nav.users"]         = "Users",
            ["nav.settings"]      = "Settings",
            ["nav.security"]      = "Security",
            ["nav.audit"]         = "Audit Log",
            ["nav.account"]       = "My Account",
            ["nav.logout"]        = "Logout",
            ["nav.theme"]         = "Theme",
            ["nav.language"]      = "Language",
            ["nav.theme.auto"]    = "Auto",
            ["nav.theme.light"]   = "Light",
            ["nav.theme.dark"]    = "Dark",

            // Roles
            ["role.Admin"]        = "Admin",
            ["role.Manager"]      = "Manager",
            ["role.Viewer"]       = "Viewer",

            // Change password (forced flow)
            ["changepw.title"]    = "Change password",
            ["changepw.prompt"]   = "Your password must be changed before you can continue.",
            ["changepw.current"]  = "Current password",
            ["changepw.new"]      = "New password",
            ["changepw.confirm"]  = "Confirm new password",
            ["changepw.submit"]   = "Save",
            ["changepw.error.missing"]  = "All fields are required.",
            ["changepw.error.mismatch"] = "New password and confirmation do not match.",
            ["changepw.error.invalid"]  = "The current password is incorrect or the new one is too weak.",

            // Users management
            ["users.title"]            = "Users",
            ["users.create"]           = "New user",
            ["users.username"]         = "Username",
            ["users.role"]             = "Role",
            ["users.status"]           = "Status",
            ["users.lastLogin"]        = "Last login",
            ["users.tempPassword"]     = "Temporary password",
            ["users.tempPasswordHint"] = "User will be forced to change this on first login.",
            ["users.generate"]         = "Generate",
            ["users.reset"]            = "Reset password",
            ["users.reset.hint"]       = "Sets a new temporary password and forces the user to change it on next login.",
            ["users.disable"]          = "Disable",
            ["users.enable"]           = "Enable",
            ["users.active"]           = "Active",
            ["users.disabled"]         = "Disabled",
            ["users.pendingPwChange"]  = "Pending password change",
            ["users.you"]              = "You",
            ["users.copyNow.title"]    = "Temporary password — copy now",
            ["users.copyNow.hint"]     = "This is the only time it will be shown. Share it with the user via a secure channel.",

            // Account page
            ["account.title"]            = "My Account",
            ["account.password"]         = "Change password",
            ["account.password.saved"]   = "Password changed successfully.",
            ["account.totp"]             = "Two-Factor Authentication (TOTP)",
            ["account.totp.hint"]        = "Use any TOTP authenticator app (Google Authenticator, Authy, 1Password, etc.).",
            ["account.totp.enabled"]     = "2FA is enabled",
            ["account.totp.disabled"]    = "2FA is disabled",
            ["account.totp.enable"]      = "Enable 2FA",
            ["account.totp.disable"]     = "Disable 2FA",
            ["account.totp.disable.label"]   = "Enter current code to disable 2FA",
            ["account.totp.disable.confirm"] = "Confirm disable",
            ["account.totp.scanHint"]    = "Scan this URI in your authenticator app or enter the secret manually:",
            ["account.totp.manualKey"]   = "Manual entry key",
            ["account.totp.confirm.label"] = "Enter the 6-digit code to confirm setup",
            ["account.totp.activate"]    = "Activate 2FA",
            ["account.totp.code.required"] = "Enter the 6-digit code from your authenticator.",

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
            ["nav.users"]         = "Uživatelé",
            ["nav.settings"]      = "Nastavení",
            ["nav.security"]      = "Zabezpečení",
            ["nav.audit"]         = "Audit",
            ["nav.account"]       = "Můj účet",
            ["nav.logout"]        = "Odhlásit",
            ["nav.theme"]         = "Vzhled",
            ["nav.language"]      = "Jazyk",
            ["nav.theme.auto"]    = "Auto",
            ["nav.theme.light"]   = "Světlý",
            ["nav.theme.dark"]    = "Tmavý",

            // Roles
            ["role.Admin"]        = "Admin",
            ["role.Manager"]      = "Manažer",
            ["role.Viewer"]       = "Prohlížeč",

            // Change password (forced flow)
            ["changepw.title"]    = "Změna hesla",
            ["changepw.prompt"]   = "Před pokračováním si musíte změnit heslo.",
            ["changepw.current"]  = "Současné heslo",
            ["changepw.new"]      = "Nové heslo",
            ["changepw.confirm"]  = "Potvrzení nového hesla",
            ["changepw.submit"]   = "Uložit",
            ["changepw.error.missing"]  = "Všechna pole jsou povinná.",
            ["changepw.error.mismatch"] = "Nové heslo a potvrzení se neshodují.",
            ["changepw.error.invalid"]  = "Současné heslo není správné nebo je nové heslo příliš slabé.",

            // Users management
            ["users.title"]            = "Uživatelé",
            ["users.create"]           = "Nový uživatel",
            ["users.username"]         = "Uživatelské jméno",
            ["users.role"]             = "Role",
            ["users.status"]           = "Stav",
            ["users.lastLogin"]        = "Poslední přihlášení",
            ["users.tempPassword"]     = "Dočasné heslo",
            ["users.tempPasswordHint"] = "Uživatel bude při prvním přihlášení nucen heslo změnit.",
            ["users.generate"]         = "Vygenerovat",
            ["users.reset"]            = "Resetovat heslo",
            ["users.reset.hint"]       = "Nastaví nové dočasné heslo a uživatel bude při příštím přihlášení nucen ho změnit.",
            ["users.disable"]          = "Deaktivovat",
            ["users.enable"]           = "Aktivovat",
            ["users.active"]           = "Aktivní",
            ["users.disabled"]         = "Deaktivovaný",
            ["users.pendingPwChange"]  = "Čeká na změnu hesla",
            ["users.you"]              = "Vy",
            ["users.copyNow.title"]    = "Dočasné heslo — zkopírovat hned",
            ["users.copyNow.hint"]     = "Heslo se zobrazí pouze jednou. Předejte ho uživateli bezpečným kanálem.",

            // Account page
            ["account.title"]            = "Můj účet",
            ["account.password"]         = "Změna hesla",
            ["account.password.saved"]   = "Heslo bylo úspěšně změněno.",
            ["account.totp"]             = "Dvoufaktorové ověření (TOTP)",
            ["account.totp.hint"]        = "Použijte libovolnou TOTP aplikaci (Google Authenticator, Authy, 1Password atd.).",
            ["account.totp.enabled"]     = "2FA je zapnuté",
            ["account.totp.disabled"]    = "2FA je vypnuté",
            ["account.totp.enable"]      = "Zapnout 2FA",
            ["account.totp.disable"]     = "Vypnout 2FA",
            ["account.totp.disable.label"]   = "Pro vypnutí 2FA zadejte aktuální kód",
            ["account.totp.disable.confirm"] = "Potvrdit vypnutí",
            ["account.totp.scanHint"]    = "Naskenujte URI v autentifikační aplikaci nebo zadejte tajný klíč ručně:",
            ["account.totp.manualKey"]   = "Klíč pro ruční zadání",
            ["account.totp.confirm.label"] = "Pro potvrzení nastavení zadejte 6místný kód",
            ["account.totp.activate"]    = "Aktivovat 2FA",
            ["account.totp.code.required"] = "Zadejte 6místný kód z autentifikační aplikace.",

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
            ["nav.users"]         = "Benutzer",
            ["nav.settings"]      = "Einstellungen",
            ["nav.security"]      = "Sicherheit",
            ["nav.audit"]         = "Audit-Log",
            ["nav.account"]       = "Mein Konto",
            ["nav.logout"]        = "Abmelden",
            ["nav.theme"]         = "Design",
            ["nav.language"]      = "Sprache",
            ["nav.theme.auto"]    = "Auto",
            ["nav.theme.light"]   = "Hell",
            ["nav.theme.dark"]    = "Dunkel",

            // Roles
            ["role.Admin"]        = "Admin",
            ["role.Manager"]      = "Manager",
            ["role.Viewer"]       = "Betrachter",

            // Change password (forced flow)
            ["changepw.title"]    = "Passwort ändern",
            ["changepw.prompt"]   = "Sie müssen Ihr Passwort ändern, bevor Sie fortfahren können.",
            ["changepw.current"]  = "Aktuelles Passwort",
            ["changepw.new"]      = "Neues Passwort",
            ["changepw.confirm"]  = "Neues Passwort bestätigen",
            ["changepw.submit"]   = "Speichern",
            ["changepw.error.missing"]  = "Alle Felder sind erforderlich.",
            ["changepw.error.mismatch"] = "Neues Passwort und Bestätigung stimmen nicht überein.",
            ["changepw.error.invalid"]  = "Das aktuelle Passwort ist falsch oder das neue Passwort ist zu schwach.",

            // Users management
            ["users.title"]            = "Benutzer",
            ["users.create"]           = "Neuer Benutzer",
            ["users.username"]         = "Benutzername",
            ["users.role"]             = "Rolle",
            ["users.status"]           = "Status",
            ["users.lastLogin"]        = "Letzte Anmeldung",
            ["users.tempPassword"]     = "Temporäres Passwort",
            ["users.tempPasswordHint"] = "Der Benutzer muss dieses bei der ersten Anmeldung ändern.",
            ["users.generate"]         = "Generieren",
            ["users.reset"]            = "Passwort zurücksetzen",
            ["users.reset.hint"]       = "Setzt ein neues temporäres Passwort und zwingt den Benutzer, es bei der nächsten Anmeldung zu ändern.",
            ["users.disable"]          = "Deaktivieren",
            ["users.enable"]           = "Aktivieren",
            ["users.active"]           = "Aktiv",
            ["users.disabled"]         = "Deaktiviert",
            ["users.pendingPwChange"]  = "Passwortwechsel ausstehend",
            ["users.you"]              = "Sie",
            ["users.copyNow.title"]    = "Temporäres Passwort — jetzt kopieren",
            ["users.copyNow.hint"]     = "Wird nur einmal angezeigt. Übermitteln Sie es dem Benutzer über einen sicheren Kanal.",

            // Account page
            ["account.title"]            = "Mein Konto",
            ["account.password"]         = "Passwort ändern",
            ["account.password.saved"]   = "Passwort erfolgreich geändert.",
            ["account.totp"]             = "Zwei-Faktor-Authentifizierung (TOTP)",
            ["account.totp.hint"]        = "Verwenden Sie eine beliebige TOTP-App (Google Authenticator, Authy, 1Password usw.).",
            ["account.totp.enabled"]     = "2FA ist aktiviert",
            ["account.totp.disabled"]    = "2FA ist deaktiviert",
            ["account.totp.enable"]      = "2FA aktivieren",
            ["account.totp.disable"]     = "2FA deaktivieren",
            ["account.totp.disable.label"]   = "Geben Sie den aktuellen Code ein, um 2FA zu deaktivieren",
            ["account.totp.disable.confirm"] = "Deaktivieren bestätigen",
            ["account.totp.scanHint"]    = "Scannen Sie diese URI in Ihrer Authenticator-App oder geben Sie den Schlüssel manuell ein:",
            ["account.totp.manualKey"]   = "Manueller Schlüssel",
            ["account.totp.confirm.label"] = "Geben Sie den 6-stelligen Code zur Bestätigung ein",
            ["account.totp.activate"]    = "2FA aktivieren",
            ["account.totp.code.required"] = "Geben Sie den 6-stelligen Code aus Ihrer App ein.",

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
