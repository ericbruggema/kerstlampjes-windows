using System.Collections.Generic;
using System.Globalization;

namespace Kerstlampjes
{
    /// <summary>
    /// Vertalingen, zelfde aanpak als in de Android-app: de Nederlandse tekst is de sleutel.
    /// Volgorde: en, de, fr, es, zh. Onbekende taal of onbekende tekst -> Nederlands.
    /// </summary>
    public static class Tr
    {
        static readonly string[] Langs = { "en", "de", "fr", "es", "zh" };
        static readonly int idx;

        static readonly Dictionary<string, string[]> T = new Dictionary<string, string[]>
        {
            { "Kerstlampjes", new[] { "Christmas Lights", "Weihnachtslichter", "Lumières de Noël", "Luces de Navidad", "圣诞彩灯" } },
            { "Lampjes aan", new[] { "Lights on", "Lichter an", "Lumières allumées", "Luces encendidas", "开灯" } },
            { "Instellingen…", new[] { "Settings…", "Einstellungen…", "Paramètres…", "Ajustes…", "设置…" } },
            { "Over Kerstlampjes…", new[] { "About Christmas Lights…", "Über Weihnachtslichter…", "À propos de Lumières de Noël…", "Acerca de Luces de Navidad…", "关于圣诞彩灯…" } },
            { "Controleren op updates", new[] { "Check for updates", "Nach Updates suchen", "Rechercher des mises à jour", "Buscar actualizaciones", "检查更新" } },
            { "Starten met Windows", new[] { "Start with Windows", "Mit Windows starten", "Démarrer avec Windows", "Iniciar con Windows", "随 Windows 启动" } },
            { "Afsluiten", new[] { "Exit", "Beenden", "Quitter", "Salir", "退出" } },
            { "Klik om de lampjes aan of uit te zetten. Rechtsklik voor het menu.",
                new[] { "Click to turn the lights on or off. Right-click for the menu.", "Klicken zum Ein-/Ausschalten. Rechtsklick für das Menü.", "Cliquez pour allumer ou éteindre. Clic droit pour le menu.", "Haz clic para encender o apagar. Clic derecho para el menú.", "单击开关彩灯，右键打开菜单。" } },
            { "Effect", new[] { "Effect", "Effekt", "Effet", "Efecto", "效果" } },
            { "Kleuren", new[] { "Colours", "Farben", "Couleurs", "Colores", "颜色" } },
            { "Snelheid", new[] { "Speed", "Geschwindigkeit", "Vitesse", "Velocidad", "速度" } },
            { "Grootte", new[] { "Size", "Größe", "Taille", "Tamaño", "大小" } },
            { "Afstand tussen lampjes", new[] { "Spacing between lights", "Abstand der Lichter", "Espacement des lumières", "Separación entre luces", "灯间距" } },
            { "Hoekafronding", new[] { "Corner rounding", "Eckenrundung", "Arrondi des coins", "Redondeo de esquinas", "圆角" } },
            { "Afstand van de rand", new[] { "Distance from the edge", "Abstand vom Rand", "Distance du bord", "Distancia al borde", "离边缘距离" } },
            { "Doorhang van de draad", new[] { "Sag of the wire", "Durchhang des Kabels", "Affaissement du fil", "Caída del cable", "电线下垂" } },
            { "Doorzichtigheid", new[] { "Opacity", "Deckkraft", "Opacité", "Opacidad", "不透明度" } },
            { "Op alle schermen", new[] { "On all screens", "Auf allen Bildschirmen", "Sur tous les écrans", "En todas las pantallas", "在所有屏幕上" } },
            { "Verbergen bij schermvullende apps (spellen, films)",
                new[] { "Hide during full-screen apps (games, films)", "Bei Vollbild-Apps ausblenden (Spiele, Filme)", "Masquer pendant les applis plein écran (jeux, films)", "Ocultar con apps a pantalla completa (juegos, películas)", "全屏应用（游戏、视频）时隐藏" } },
            { "Standaardwaarden", new[] { "Defaults", "Standardwerte", "Valeurs par défaut", "Valores predeterminados", "恢复默认" } },
            { "Sluiten", new[] { "Close", "Schließen", "Fermer", "Cerrar", "关闭" } },
            { "Vast aan", new[] { "Steady", "Dauerlicht", "Fixe", "Fijo", "常亮" } },
            { "Fonkelen", new[] { "Twinkle", "Funkeln", "Scintillement", "Parpadeo suave", "闪烁" } },
            { "Om en om knipperen", new[] { "Alternate blinking", "Abwechselnd blinken", "Clignotement alterné", "Parpadeo alterno", "交替闪烁" } },
            { "Lopend licht", new[] { "Chasing light", "Lauflicht", "Lumière chenillard", "Luz corredora", "跑马灯" } },
            { "Kleurgolf", new[] { "Colour wave", "Farbwelle", "Vague de couleurs", "Ola de color", "彩色波浪" } },
            { "Klassiek bont", new[] { "Classic multicolour", "Klassisch bunt", "Multicolore classique", "Multicolor clásico", "经典彩色" } },
            { "Warm wit", new[] { "Warm white", "Warmweiß", "Blanc chaud", "Blanco cálido", "暖白" } },
            { "Rood / groen / goud", new[] { "Red / green / gold", "Rot / Grün / Gold", "Rouge / vert / or", "Rojo / verde / dorado", "红 / 绿 / 金" } },
            { "IJsblauw / wit", new[] { "Ice blue / white", "Eisblau / Weiß", "Bleu glace / blanc", "Azul hielo / blanco", "冰蓝 / 白" } },
            { "Roze / paars", new[] { "Pink / purple", "Rosa / Lila", "Rose / violet", "Rosa / morado", "粉 / 紫" } },
            { "uit", new[] { "off", "aus", "éteint", "apagado", "已关" } },
            { "Over", new[] { "About", "Über", "À propos", "Acerca de", "关于" } },
            { "Een snoer kerstlampjes langs de rand van je scherm. Het blijft altijd zichtbaar, klikt niet in de weg en zit in het systeemvak (rechtsonder, bij de klok): daar zet je het uit, past de instellingen aan en lees je dit.",
                new[] { "A string of Christmas lights along the edge of your screen. It stays visible, never gets in the way of clicks and lives in the system tray (bottom right, near the clock): turn it off there, change the settings and read this.",
                        "Eine Lichterkette am Rand deines Bildschirms. Sie bleibt immer sichtbar, stört keine Klicks und sitzt im Infobereich (unten rechts, bei der Uhr): dort schaltest du sie aus, änderst die Einstellungen und liest dies.",
                        "Une guirlande lumineuse le long du bord de votre écran. Elle reste toujours visible, ne gêne pas les clics et se trouve dans la zone de notification (en bas à droite, près de l'horloge) : vous pouvez l'éteindre, régler les paramètres et lire ceci.",
                        "Una guirnalda de luces a lo largo del borde de tu pantalla. Siempre visible, no estorba los clics y vive en la bandeja del sistema (abajo a la derecha, junto al reloj): ahí la apagas, cambias los ajustes y lees esto.",
                        "屏幕边缘的一串圣诞彩灯。它始终可见，不会挡住点击，并位于系统托盘（右下角时钟旁）：在那里可以关闭、调整设置和阅读本说明。" } },
            { "Gratis en open source (MIT-licentie). Gebruik je code of ideeën uit dit project? Vermeld dan een verwijzing naar:",
                new[] { "Free and open source (MIT licence). Using code or ideas from this project? Please credit it with a link to:",
                        "Kostenlos und Open Source (MIT-Lizenz). Du verwendest Code oder Ideen aus diesem Projekt? Dann nenne bitte einen Verweis auf:",
                        "Gratuit et open source (licence MIT). Vous utilisez du code ou des idées de ce projet ? Merci d'indiquer un lien vers :",
                        "Gratis y de código abierto (licencia MIT). ¿Usas código o ideas de este proyecto? Menciona un enlace a:",
                        "免费开源（MIT 许可证）。如果使用了本项目的代码或创意，请注明并链接到：" } },
            { "Er gaat niets via internet, behalve als je zelf op “Controleren op updates” klikt.",
                new[] { "Nothing goes over the internet, except when you click “Check for updates”.", "Nichts geht über das Internet, außer wenn du auf „Nach Updates suchen“ klickst.", "Rien ne passe par internet, sauf si vous cliquez sur « Rechercher des mises à jour ».", "Nada pasa por internet, salvo cuando pulsas «Buscar actualizaciones».", "除非你点击“检查更新”，否则不会联网。" } },
            { "Er is een nieuwe versie: {0} (jij hebt {1}).\n\nDownloadpagina openen?",
                new[] { "There is a new version: {0} (you have {1}).\n\nOpen the download page?", "Es gibt eine neue Version: {0} (du hast {1}).\n\nDownloadseite öffnen?", "Une nouvelle version est disponible : {0} (vous avez {1}).\n\nOuvrir la page de téléchargement ?", "Hay una versión nueva: {0} (tienes {1}).\n\n¿Abrir la página de descarga?", "有新版本：{0}（你的版本是 {1}）。\n\n打开下载页面？" } },
            { "Je hebt de nieuwste versie ({0}).",
                new[] { "You have the latest version ({0}).", "Du hast die neueste Version ({0}).", "Vous avez la dernière version ({0}).", "Tienes la última versión ({0}).", "你使用的已是最新版本（{0}）。" } },
            { "Controleren is niet gelukt. Ben je online?",
                new[] { "Checking failed. Are you online?", "Die Prüfung ist fehlgeschlagen. Bist du online?", "La vérification a échoué. Êtes-vous en ligne ?", "No se pudo comprobar. ¿Estás en línea?", "检查失败。你是否已联网？" } },
            { "Versie", new[] { "Version", "Version", "Version", "Versión", "版本" } },
            { "Website en broncode", new[] { "Website and source code", "Website und Quellcode", "Site web et code source", "Sitio web y código fuente", "网站与源代码" } },
        };

        static Tr()
        {
            string lg = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName;
            // -1 = Nederlands; onbekende taal -> Engels
            idx = lg == "nl" ? -1 : System.Math.Max(0, System.Array.IndexOf(Langs, lg));
        }

        public static string t(string nl)
        {
            string[] v;
            if (idx >= 0 && T.TryGetValue(nl, out v)) return v[idx];
            return nl;
        }

        public static string f(string nl, params object[] args) { return string.Format(t(nl), args); }
    }
}
