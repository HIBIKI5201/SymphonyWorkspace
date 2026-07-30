using System;

namespace SymphonyFrameWork.Exceptions
{
    /// <summary> Symphony Frameworkの初期化前に機能へアクセスした場合に発生する例外。 </summary>
    public sealed class SymphonyNotInitializedException : Exception
    {
        /// <summary> 既定のメッセージで例外を生成する。 </summary>
        public SymphonyNotInitializedException() : base("SymphonyFrameworkが未初期化です。")
        {
        }

        /// <summary> 未初期化だった機能の型を含むメッセージで例外を生成する。 </summary>
        /// <param name="type"> 未初期化だった機能の型。 </param>
        public SymphonyNotInitializedException(Type type) : base($"[{type.Name}] SymphonyFrameworkが未初期化です。")
        {
        }

        /// <summary> 指定したメッセージで例外を生成する。 </summary>
        /// <param name="message"> エラーの詳細。 </param>
        public SymphonyNotInitializedException(string message) : base(message)
        {
        }

        /// <summary> 指定したメッセージと原因例外で例外を生成する。 </summary>
        /// <param name="message"> エラーの詳細。 </param>
        /// <param name="innerException"> 原因となった例外。 </param>
        public SymphonyNotInitializedException(string message, Exception innerException) : base(message, innerException)
        {
        }
    }
}
