using System.Text.RegularExpressions;
using UnityEngine;

namespace SymphonyFrameWork
{
    /// <summary>
    ///     文字列のユーティリティクラス。
    /// </summary>
    public static class SymphonyStringUtil
    {
        /// <summary>
        ///     リッチテキストにカラータグを挿入する。
        /// </summary>
        /// <param name="text"> 装飾する文字列。 </param>
        /// <param name="color"> 適用する文字色。 </param>
        /// <returns> colorタグで囲んだ文字列。 </returns>
        public static string AddRichTextColor(this string text, Color color)
        {
            // ColorをRGBに変換する。
            string htmlColor = ColorUtility.ToHtmlStringRGB(color);
            return $"<color=#{htmlColor}>{text}</color>"; // タグに入れて返す。
        }

        /// <summary>
        ///     一番外側の &lt;color&gt; タグを削除する。
        ///     多重の場合は最外層のみ。
        /// </summary>
        public static string RemoveRichTextColor(this string text)
        {
            // 正規表現でカラーのタグを検索。
            Regex outerColorRegex = new Regex(@"^<color=[^>]+>(.*)</color>$", RegexOptions.Singleline);
            var match = outerColorRegex.Match(text);

            if (match.Success)
            {
                // キャプチャされた中身を返す（外側のタグ除去）
                return match.Groups[1].Value;
            }

            // colorタグで囲まれていなければそのまま返す
            return text;
        }

        /// <summary>
        ///     リッチテキストに太文字タグを挿入する。
        /// </summary>
        /// <param name="text"> 装飾する文字列。 </param>
        /// <returns> bタグで囲んだ文字列。 </returns>
        public static string AddRichTextBold(this string text) => $"<b>{text}</b>";

        /// <summary>
        ///     一番外側の &lt;b&gt; タグを削除する。
        ///     多重の場合は最外層のみ。
        /// </summary>
        /// <param name="text"> 最外層のbタグを除去する文字列。 </param>
        /// <returns> 最外層のbタグを除去した文字列。 </returns>
        public static string RemoveRichTextBold(this string text)
        {
            // 正規表現で太文字のタグを検索。
            Regex outerBoldRegex = new Regex(@"^<b>(.*)</b>$", RegexOptions.Singleline);
            var match = outerBoldRegex.Match(text);

            if (match.Success)
            {
                // キャプチャされた中身を返す（外側のタグ除去）
                return match.Groups[1].Value;
            }

            // bタグで囲まれていなければそのまま返す
            return text;
        }

        /// <summary>
        ///     リッチテキストに下線タグを挿入する。
        /// </summary>
        /// <param name="text"> 装飾する文字列。 </param>
        /// <returns> uタグで囲んだ文字列。 </returns>
        public static string AddRichTextUnderline(this string text) => $"<u>{text}</u>";

        /// <summary>
        ///     一番外側の &lt;u&gt; タグを削除する。
        ///     多重の場合は最外層のみ。
        /// </summary>
        /// <param name="text"> 最外層のuタグを除去する文字列。 </param>
        /// <returns> 最外層のuタグを除去した文字列。 </returns>
        public static string RemoveRichTextUnderline(this string text)
        {
            // 正規表現で下線のタグを検索。
            Regex outerUnderlineRegex = new Regex(@"^<u>(.*)</u>$", RegexOptions.Singleline);
            var match = outerUnderlineRegex.Match(text);

            if (match.Success)
            {
                // キャプチャされた中身を返す（外側のタグ除去）
                return match.Groups[1].Value;
            }

            // uタグで囲まれていなければそのまま返す
            return text;
        }
    }
}
