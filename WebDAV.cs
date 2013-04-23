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
	/// WebDAVConnect�N���X
	/// </summary>
	public class WebDAVConnect
	{
		#region �N���X�萔
		
		// �p�X�Z�p���[�^������
		private readonly string PATH_SEPARATOR = Path.DirectorySeparatorChar.ToString();	

		// ���C���h�J�[�h������
		private const string WILD_CARD = @"*";

		// URL�Z�p���[�^������
		private const string URI_SEPARATOR = @"/";

		// �G���R�[�h
		private const string ENCODE = "shift_jis";

		// �t�@�C�����X�g�e�[�u����
		private const string COL_NAME = "NAME";
		private const string COL_TYPE = "TYPE";

		// HTTP/WebDAV�v���g�R�����\�b�h
		private const string HTTP_METHOD_GET	= "GET";
		private const string HTTP_METHOD_PUT	= "PUT";
		private const string WEBDAV_METHOD_PROPFIND	= "PROPFIND";

		#endregion

		#region �N���X�ϐ�
		private int timeOutSec;		// �^�C���A�E�g���ԁi�b�j
		private int retryCount;		// ���g���C��
		private int retryInterval;	// ���g���C���̃C���^�[�o���i�b�j
		private Encoding encoding;	// �G���R�[�h
		#endregion


		// public ���\�b�h

		#region �R���X�g���N�^
		/// <summary>
		/// �R���X�g���N�^
		/// </summary>
        public WebDAVConnect()
		{
			this.encoding = Encoding.GetEncoding(ENCODE);
		}
		#endregion

		#region �t�@�C���擾
		public void GetFile(string serverPath, string localPath)
		{
			// �ڑ�����ݒ�
            this.setConnectInfo();

			// �T�[�o�p�X�̖�����'/'�łȂ���Βǉ�
			if(!serverPath.EndsWith(URI_SEPARATOR))
			{
				serverPath += URI_SEPARATOR;
			}

			// ���[�J���p�X�̖�����'\'�łȂ���Βǉ�
			if(!localPath.EndsWith(PATH_SEPARATOR))
			{
				localPath += PATH_SEPARATOR;
			}

            // �_�E�����[�h�Ώۃt�@�C�����X�g�擾
			DataTable table = this.getFileList(serverPath);

			// �_�E�����[�h
			if(table != null)
			{
				DataRow[] drs = table.Select();
				foreach(DataRow dr in drs)
				{
					string serverFileName = serverPath + URI_SEPARATOR + dr[COL_NAME].ToString();
					string localFileName = localPath + dr[COL_NAME].ToString();

					// �f�[�^�t�@�C���_�E�����[�h
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

	
		// private ���\�b�h

		#region �ڑ�����ݒ�
		private string setConnectInfo()
		{
			this.timeOutSec = 30;

            this.retryCount = 3;

            this.retryInterval = 3;

            return null;
		}
		#endregion

		#region �t�@�C�����X�g�擾
		private DataTable getFileList(string serverPath)
		{
			XmlDocument doc = new XmlDocument();

			doc = this.propFind(serverPath);

			XmlElement root = doc.DocumentElement;

			// �v���t�B�b�N�X�擾
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

		#region �t�@�C�����X�g�擾�R�A����
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
					// PROPFIND�R�}���h�쐬
					string strData = this.createPROPFINDCmd();

					// HTTP�w�b�_���
					webReq = (HttpWebRequest)WebRequest.Create(serverPath);
					webReq.KeepAlive = true;
					webReq.Headers.Set("Pragma", "no-cache");
					webReq.Headers.Set("Depth", "1,noroot");
					webReq.ContentType =  "text/xml";

					// �^�C���A�E�g����
					webReq.Timeout = this.timeOutSec * 1000;
					// ���N�G�X�g�R�}���h
					webReq.Method = WEBDAV_METHOD_PROPFIND;

					// �N�G�����o�C�g�z��ɂ���
					byteData = this.encoding.GetBytes(strData);
					webReq.ContentLength = byteData.Length;

					// ���N�G�X�g�X�g���[��
					sendStrm = webReq.GetRequestStream();
					sendStrm.Write(byteData, 0, byteData.Length);
					sendStrm.Close();

					// ���N�G�X�g���M
					webRes = (HttpWebResponse)webReq.GetResponse();

					// ���ʃR�[�h�擾
					int iStatCode =  (int)webRes.StatusCode;
					string sStatus = iStatCode.ToString();

					// ���ʎ�M
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

		#region ��M�R�A����
		private void get(string serverPath, string localPath)
		{
			// WEB���N�G�X�g
			WebRequest webReq = null;
			// ���X�|���X
			WebResponse webRes = null;
			// ��M�X�g���[��
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

					// ���[�J���t�@�C������
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

		#region ���M�R�A����
		private void put(string localPath, string serverPath)
		{
			byte[] byteData = null;
			StreamReader sr = null;
			Stream dataStream = null;
			HttpWebRequest webReq = null;
			HttpWebResponse webRes = null;

			// ���[�J���t�@�C���Ǎ�
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
					// HTTP���N�G�X�g�쐬
					webReq = (HttpWebRequest)HttpWebRequest.Create(serverPath); 
					webReq.Method = HTTP_METHOD_PUT; 
					webReq.ContentType = "text/plain";
					webReq.ContentLength = byteData.Length; 
					webReq.KeepAlive = true; 
					webReq.Timeout = this.timeOutSec * 1000;

					// ���M�X�g���[��
					dataStream = webReq.GetRequestStream(); 
					dataStream.Write(byteData, 0, byteData.Length); 
					dataStream.Close(); 

					// ���X�|���X�擾
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

		#region PROPFIND�R�}���h�쐬
		private string createPROPFINDCmd()
		{
			return "<?xml version=\"1.0\" encoding=\"utf-8\" ?><propfind xmlns=\"DAV:\">  <prop>\t<displayname/>\t<iscollection/>  </prop></propfind>";
		}
		#endregion
	}
}