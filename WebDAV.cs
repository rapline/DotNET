using System;
using System.Net;
using System.Data;
using System.Text;
using System.Diagnostics;
using System.IO;
using System.Xml;
using System.Text.RegularExpressions;
using System.Threading;

namespace WebDAV
{
	/// <summary>
	/// WebDAVConnectクラス
	/// </summary>
	public class WebDAVConnect
	{
		#region クラス定数
		
		// パスセパレータ文字列
		private readonly string PATH_SEPARATOR = Path.DirectorySeparatorChar.ToString();	

		// ワイルドカード文字列
		private const string WILD_CARD = @"*";

		// URLセパレータ文字列
		private const string URI_SEPARATOR = @"/";

		// エンコード
		private const string ENCODE = "shift_jis";

		// ファイルリストテーブル列名
		private const string COL_NAME = "NAME";
		private const string COL_TYPE = "TYPE";

		// HTTP/WebDAVプロトコルメソッド
		private const string HTTP_METHOD_GET	= "GET";
		private const string HTTP_METHOD_PUT	= "PUT";
		private const string WEBDAV_METHOD_PROPFIND	= "PROPFIND";

		#endregion

		#region クラス変数
		private int timeOutSec;		// タイムアウト時間（秒）
		private int retryCount;		// リトライ回数
		private int retryInterval;	// リトライ時のインターバル（秒）
		private Encoding encoding;	// エンコード
		#endregion


		// public メソッド

		#region コンストラクタ
		/// <summary>
		/// コンストラクタ
		/// </summary>
        public WebDAVConnect()
		{
			this.encoding = Encoding.GetEncoding(ENCODE);
		}
		#endregion

		#region ファイル取得
		public void GetFile(string serverPath, string localPath)
		{
			// 接続情報を設定
            this.setConnectInfo();

			// サーバパスの末尾が'/'でなければ追加
			if(!serverPath.EndsWith(URI_SEPARATOR))
			{
				serverPath += URI_SEPARATOR;
			}

			// ローカルパスの末尾が'\'でなければ追加
			if(!localPath.EndsWith(PATH_SEPARATOR))
			{
				localPath += PATH_SEPARATOR;
			}

            // ダウンロード対象ファイルリスト取得
			DataTable table = this.getFileList(serverPath);

			// ダウンロード
			if(table != null)
			{
				DataRow[] drs = table.Select();
				foreach(DataRow dr in drs)
				{
					string serverFileName = serverPath + URI_SEPARATOR + dr[COL_NAME].ToString();
					string localFileName = localPath + dr[COL_NAME].ToString();

					// データファイルダウンロード
					try
					{
						this.get(serverFileName, localFileName);
					}
					catch(System.Exception ex)
					{
						throw ex;
					}
				}
			}
		}
		#endregion

	
		// private メソッド

		#region 接続情報を設定
		private string setConnectInfo()
		{
			this.timeOutSec = 30;

            this.retryCount = 3;

            this.retryInterval = 3;

            return null;
		}
		#endregion

		#region ファイルリスト取得
		private DataTable getFileList(string serverPath)
		{
			XmlDocument doc = new XmlDocument();

			doc = this.propFind(serverPath);

			XmlElement root = doc.DocumentElement;

			// プレフィックス取得
			string prefix = root.Prefix;

			XmlNamespaceManager nsmgr = new XmlNamespaceManager(doc.NameTable);
			if(prefix != string.Empty)
			{
				nsmgr.AddNamespace(prefix, "DAV:");
				prefix += ":";
			}

			DataTable table = new DataTable();
			table.Columns.Add(COL_NAME, Type.GetType("System.String"));
			table.Columns.Add(COL_TYPE, Type.GetType("System.String"));

			string selectStr = "/" + prefix + "multistatus/" + prefix + "response/" + prefix + "href";
			XmlNodeList list = doc.SelectNodes(selectStr, nsmgr);
			foreach(XmlNode node in list)
			{
				selectStr = "/" + prefix + "multistatus/" + prefix + "response[" + prefix
					+	"href='" + node.InnerText + "']/" + prefix + "propstat/" + prefix + "prop";

				XmlNode xn = doc.SelectSingleNode(selectStr, nsmgr);
				string isFolder = (xn[prefix + "iscollection"].InnerText);
				string name = (xn[prefix + "displayname"].InnerText);

				DataRow row = table.NewRow();
				row[COL_NAME] = name;
				row[COL_TYPE] = isFolder;
				table.Rows.Add(row);
			}

			return table;
		}
		#endregion

		#region ファイルリスト取得コア処理
		private XmlDocument propFind(string serverPath)
		{
			HttpWebRequest webReq = null;
			byte[] byteData = null;
			Stream sendStrm = null;
			HttpWebResponse webRes = null;
			Stream rtnStrm = null;
			XmlDocument doc = null;
			
			for(int i = 0; i <= this.retryCount; i++)
			{
				try
				{
					// PROPFINDコマンド作成
					string strData = this.createPROPFINDCmd();

					// HTTPヘッダ情報
					webReq = (HttpWebRequest)WebRequest.Create(serverPath);
					webReq.KeepAlive = true;
					webReq.Headers.Set("Pragma", "no-cache");
					webReq.Headers.Set("Depth", "1,noroot");
					webReq.ContentType =  "text/xml";

					// タイムアウト時間
					webReq.Timeout = this.timeOutSec * 1000;
					// リクエストコマンド
					webReq.Method = WEBDAV_METHOD_PROPFIND;

					// クエリをバイト配列にする
					byteData = this.encoding.GetBytes(strData);
					webReq.ContentLength = byteData.Length;

					// リクエストストリーム
					sendStrm = webReq.GetRequestStream();
					sendStrm.Write(byteData, 0, byteData.Length);
					sendStrm.Close();

					// リクエスト送信
					webRes = (HttpWebResponse)webReq.GetResponse();

					// 結果コード取得
					int iStatCode =  (int)webRes.StatusCode;
					string sStatus = iStatCode.ToString();

					// 結果受信
					rtnStrm = webRes.GetResponseStream();

					doc = new XmlDocument();
					doc.Load(rtnStrm);

					rtnStrm.Close();
					webRes.Close();

					break;
				}
				catch(WebException ex)
				{
					if(i < this.retryCount)
					{
						Thread.Sleep(this.retryInterval * 1000);	
					}
					else
					{
						throw ex;
					}
				}
				finally
				{
					if(sendStrm != null) sendStrm.Close();
					if(webRes != null) webRes.Close();
					if(rtnStrm != null) rtnStrm.Close();
				}
			}

			return doc;
		}
		#endregion

		#region 受信コア処理
		private void get(string serverPath, string localPath)
		{
			// WEBリクエスト
			WebRequest webReq = null;
			// レスポンス
			WebResponse webRes = null;
			// 受信ストリーム
			Stream strm = null;

			StreamReader sr = null;
			StreamWriter sw =null;

			for(int i = 0; i <= this.retryCount; i++)
			{
				try
				{
					webReq = HttpWebRequest.Create(serverPath);
					webReq.Method = HTTP_METHOD_GET; 
					webReq.Timeout = this.timeOutSec * 1000;
					webRes = webReq.GetResponse();
					strm = webRes.GetResponseStream();

					sr = new StreamReader(strm, this.encoding);
					string data = sr.ReadToEnd();
					sr.Close();

					// ローカルファイル書込
					sw = new StreamWriter(localPath, false, this.encoding);
					sw.Write(data);
					sw.Close();

					break;
				}
				catch(WebException ex)
				{
					if(i < this.retryCount)
					{
						Thread.Sleep(this.retryInterval * 1000);	
					}
					else
					{
						throw ex;
					}
				}
				finally
				{
					if(sr != null) sr.Close();
					if(webRes != null) webRes.Close();
					if(strm != null) strm.Close();
					if(sw != null) sw.Close();
				}
			}
		}
		#endregion

		#region 送信コア処理
		private void put(string localPath, string serverPath)
		{
			byte[] byteData = null;
			StreamReader sr = null;
			Stream dataStream = null;
			HttpWebRequest webReq = null;
			HttpWebResponse webRes = null;

			// ローカルファイル読込
			byteData = null;
			if(localPath != null && localPath.Length > 0)
			{
				FileInfo fi = new FileInfo(localPath);
				sr = new StreamReader(fi.FullName, this.encoding);
				string strData = sr.ReadToEnd();
				sr.Close();
				byteData = this.encoding.GetBytes(strData); 
			}
			else
			{
				byteData = this.encoding.GetBytes(string.Empty);
			}

			for(int i = 0; i <= this.retryCount; i++)
			{
				try
				{
					// HTTPリクエスト作成
					webReq = (HttpWebRequest)HttpWebRequest.Create(serverPath); 
					webReq.Method = HTTP_METHOD_PUT; 
					webReq.ContentType = "text/plain";
					webReq.ContentLength = byteData.Length; 
					webReq.KeepAlive = true; 
					webReq.Timeout = this.timeOutSec * 1000;

					// 送信ストリーム
					dataStream = webReq.GetRequestStream(); 
					dataStream.Write(byteData, 0, byteData.Length); 
					dataStream.Close(); 

					// レスポンス取得
					webRes = (HttpWebResponse)webReq.GetResponse();
					string response = webRes.StatusCode.ToString(); 
					webRes.Close();

					break;
				}
				catch(WebException ex)
				{
					if(i < this.retryCount)
					{
						Thread.Sleep(this.retryInterval * 1000);	
					}
					else
					{
						throw ex;
					}
				}
				finally
				{
					if(sr != null) sr.Close();
					if(dataStream != null) dataStream.Close();
					if(webRes != null) webRes.Close();
				}
			}
		}
		#endregion

		#region PROPFINDコマンド作成
		private string createPROPFINDCmd()
		{
			return "<?xml version=\"1.0\" encoding=\"utf-8\" ?><propfind xmlns=\"DAV:\">  <prop>\t<displayname/>\t<iscollection/>  </prop></propfind>";
		}
		#endregion
	}
}