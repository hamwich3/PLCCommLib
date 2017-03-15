using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace PLCLib
{

	/// <summary>
	/// Provides client connections and protocol for WinPLCs.
	/// </summary>
	public class PLC
	{
		TcpClient tcp;
		NetworkStream stream;

		public List<Tag> Tags = new List<Tag>();
		Dictionary<string, int> getTagsByString = new Dictionary<string, int>();
		Dictionary<Int16[], int> getTagsByNumber = new Dictionary<Int16[], int>();

		public string ip = "";
		public string Name = "";
		public byte[] projectName;
		public Boolean isConnected = false;
		public Boolean plcFound = false;

		/// <summary>
		/// Finds and connects specified PLC on the network.
		/// </summary>
		/// <param name="plcName">Name of PLC</param>
		/// <exception cref="System.Exception">Thrown when PLC is not found.</exception>
		public async Task Connect(string plcName)
		{
			DiscoverPLC find = new DiscoverPLC();
			find.Discover();
			FoundPLC foundplc = find.getPLC(plcName);
			if (foundplc == null) throw new Exception("PLC not found: " + plcName);
			Name = plcName;
			plcFound = true;
			ip = foundplc.ipEndPoint.Address.ToString();
			tcp = new TcpClient(ip, 2002);
			stream = tcp.GetStream();
			stream.ReadTimeout = 10000;
			stream.WriteTimeout = 10000;
			await ReadProjectNameAsync();
			isConnected = true;
		}

		/// <summary>
		/// Closes and disposes NetworkStream and TCPClient if open.
		/// </summary>
		public void Disconnect()
		{
			if (stream != null)
			{
				stream.Close();
				stream.Dispose();
			}
			if (tcp != null)
			{
				tcp.Close();
			}
			isConnected = false;
		}

		/// <summary>
		/// Reads project name from PLC into byte[] PLC.projectname
		/// </summary>
		/// <exception cref="System.Exception">Thrown on third failure to read name.</exception>
		public async Task ReadProjectNameAsync(int retries = 0)
		{
			// Init read command bytes (12)
			byte[] sendBuf = { 12, 0, 0, 0, 0, 0, 0, 0, 64, 0, 0, 0 };

			// Read buffer
			byte[] inBuf = new byte[35];

			// Write command to stream
			await stream.WriteAsync(sendBuf, 0, 12);
			bool tryAgain = false;
			try
			{
				int len = await stream.ReadAsync(inBuf, 0, 35);
				projectName = new byte[((len - 12) / 2)];

				// Get Project Name from inBytes
				for (int i = 12, j = 0; i < len; i += 2, j++)
				{
					projectName[j] = inBuf[i];
				}
			}
			catch (Exception e)
			{
				if (retries < 3)
				{
					tryAgain = true;
				}
				else throw new Exception("Could not get project name!");
				System.Windows.Forms.MessageBox.Show(e.ToString());
			}

			if (tryAgain)
			{
				await ReadProjectNameAsync(++retries);
			}

			// for testing
			string s = Encoding.UTF8.GetString(projectName, 0, projectName.Length);
			Console.WriteLine(s);
		}


		/// <summary>
		/// Returns all PLC information.
		/// </summary>
		public async Task<byte[]> ReadProjectInfoAsync()
		{
			await requestProjectInfoAsync();
			return await recieveProjectInfoAsync();
		}


		/// <summary>
		/// Sends PLC request for all information.
		/// </summary>
		private async Task requestProjectInfoAsync(byte b = 1)
		{
			// follow up with RecieveProjectInfo()

			// Number of data bytes
			byte Nb = (byte)((projectName.Length * 2) + 24);
			byte l = (byte)(projectName.Length + 4);
			byte[] sendBuf = new byte[Nb];
			sendBuf[0] = Nb;
			sendBuf[8] = 61;
			sendBuf[12] = l;
			int i, j = 0;
			for (i = 0, j = 0; j < projectName.Length; i += 2, j++)
			{
				sendBuf[14 + i] = projectName[j];
			}
			sendBuf[14 + i] = 46;  // .
			sendBuf[16 + i] = 109; // m
			sendBuf[18 + i] = 97;  // a
			sendBuf[20 + i] = 112; // p
			sendBuf[22 + i] = b;

			await stream.WriteAsync(sendBuf, 0, Nb);
		}


		/// <summary>
		/// Reads and assembles PLC information packets.
		/// </summary>
		private async Task<byte[]> recieveProjectInfoAsync()
		{
			byte[] inBuf = new byte[1040];

			int len = await stream.ReadAsync(inBuf, 0, 1040);

			if (len >= 1040)
			{
				await requestProjectInfoAsync(0);
				byte[] _inBuf = await recieveProjectInfoAsync();
				byte[] sizedBuf = new byte[_inBuf.Length + inBuf.Length];
				Array.Copy(inBuf, 0, sizedBuf, 0, inBuf.Length);
				Array.Copy(_inBuf, 16, sizedBuf, len, _inBuf.Length - 16);

				return sizedBuf;
			}

			byte[] recieved = new byte[len];
			Array.Copy(inBuf, recieved, len);
			return recieved;
		}

		/// <summary>
		/// Pings plc.
		/// </summary>
		public async Task<bool> PingPLCAsync()
		{
			Ping pingSender = new Ping();
			PingReply reply = await pingSender.SendPingAsync(ip, 100);

			if (reply.Status == IPStatus.Success)
			{
				return true;
			}
			else
			{
				return false;
			}
		}

		/// <summary>
		/// Sends PLC Run command.
		/// </summary>
		public async Task RunProjectAsync()
		{
			if (projectName == null)
			{
				try
				{
					await ReadProjectNameAsync();
				}
				catch (Exception e)
				{
					throw e;
				}
			}
			byte Nb = (byte)((projectName.Length * 2) + 12);
			byte[] run = new byte[Nb];
			run[0] = Nb;
			run[8] = 17;
			int i, j = 0;
			for (i = 0, j = 0; j < projectName.Length; i += 2, j++)
			{
				run[12 + i] = projectName[j];
			}
			await stream.WriteAsync(run, 0, Nb);

			byte[] inBuf = new byte[12];
			try
			{
				await stream.ReadAsync(inBuf, 0, 12);
			}
			catch (Exception)
			{
				//do nothing; doesn't matter
			}
		}

		/// <summary>
		/// Sends PLC Stop command.
		/// </summary>
		public async Task StopProjectAsync()
		{
			byte[] stop = { 0x0c, 0, 0, 0, 0, 0, 0, 0, 0x0b, 0, 0, 0 };
			await stream.WriteAsync(stop, 0, 12);

			byte[] inBuf = new byte[12];
			try
			{
				await stream.ReadAsync(inBuf, 0, 12);
			}
			catch (Exception)
			{
				//do nothing; doesn't matter
			}
		}

		/// <summary>
		/// Adds tag to Read/Write list. "tagName" does not have to match PLC tag name.
		/// </summary>
		/// <param name="type"></param>
		/// <param name="tagNumber"></param>
		/// <param name="name"></param>
		public void addTag(tType tagType, Int16 tagNumber, string tagName)
		{
			Tag tag = new Tag(tagType, tagNumber, tagName);
			if (Tags.Contains(tag))
			{
				throw new Exception("Tag already exists!");
			}
			Tags.Add(tag);
			Int16[] taginfo = { (byte)tagType, tagNumber };
			try
			{
				getTagsByNumber.Add(taginfo, Tags.IndexOf(tag));
			}
			catch (Exception)
			{
				Tags.Remove(tag);
				throw new Exception("Tag with that number and type already exists!");
			}
			try
			{
				getTagsByString.Add(tagName, Tags.IndexOf(tag));
			}
			catch (Exception)
			{
				Tags.Remove(tag);
				throw new Exception("Tag with that name already Exists!");
			}
		}

		/*
		 * 
		 * 
		 * 
		 * v--------------------------Read Methods---------------------------v
		 * 
		 * 
		 * 
		 * */

		public async Task<bool> IsRunningAsync()
		{
			await requestIsRunningAsync();
			byte[] tagArray = await readBufferAsync();
			byte[] running = { 0x0E, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00 };
			byte[] stopped = { 0x0C, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0xFE, 0xFF, 0xFF, 0xFF };
			if (Enumerable.SequenceEqual(tagArray, stopped)) return false;
			else return true;
		}

		private async Task requestIsRunningAsync()
		{
			Int16 Nb; // Number of bytes in data
			byte Nbh; // Nb high byte
			byte Nbl; // nb low byte
			Int16 Nt; // Number of tags to read
			byte Ntl; // Nt low byte
			byte Nth; // Nt high byte

			Nt = (Int16)0;
			Ntl = (byte)Nt;
			Nth = (byte)((Nt & 0xFF00) >> 8);
			Nb = (Int16)(14);
			Nbl = (byte)Nb;
			Nbh = (byte)((Nb & 0xFF00) >> 8);

			// First 14 bytes
			byte[] sendBuf = new byte[Nb];
			sendBuf[0] = Nbl;   // total number of data bytes
			sendBuf[1] = Nbh;   //
			sendBuf[8] = 3;     // 0x03 is required for read for some reason
			await stream.WriteAsync(sendBuf, 0, Nb);
		}

		/// <summary>
		/// Reads all tags in list.
		/// </summary>
		public async Task ReadTagsAsync()
		{
			await requestReadTagsAsync();
			byte[] tagArray = await readBufferAsync();
			List<Tag> Strings = new List<Tag>();

			for (int j = 0; j < Tags.Count; j++)
			{
				Tag t = Tags.ElementAt(j);
				int i = (6 * j) + 14;
				Array.Copy(tagArray, i, t.ByteValue, 0, 4);

				if (t.type == tType.StringType) Strings.Add(t);
			}

			foreach (Tag s in Strings)
			{
				await readStringAsync(s);
			}
		}

		/* Sends packet requesting tag values, using the structure:
		 * 
		 * 2 bytes for total number of data bits in packet, followed by 6 nulls,
		 * "3" followed by 3 nulls, then 2 bytes for number of tags requested
		 * {Nb, nB, 0 , 0 , 0 , 0 , 0 , 0 , }
		 * {3 , 0 , 0 , 0 , Nt, Nt,} (14 bytes)
		 * Repeated for each tag: 
		 * Byte for tag type followed by null
		 * 2 bytes for tag number
		 * {Tt, 0 , Tn, Tn, } (4 bytes each)
		 * */
		/// <summary>
		/// Requests tag values.
		/// </summary>
		private async Task requestReadTagsAsync()
		{
			Int16 Nb; // Number of bytes in data
			byte Nbh; // Nb high byte
			byte Nbl; // nb low byte
			Int16 Nt; // Number of tags to read
			byte Ntl; // Nt low byte
			byte Nth; // Nt high byte

			Nt = (Int16)Tags.Count;
			Ntl = (byte)Nt;
			Nth = (byte)((Nt & 0xFF00) >> 8);
			Nb = (Int16)((Nt * 4) + 14);
			Nbl = (byte)Nb;
			Nbh = (byte)((Nb & 0xFF00) >> 8);

			// First 14 bytes
			byte[] sendBuf = new byte[Nb];
			sendBuf[0] = Nbl;   // total number of data bytes
			sendBuf[1] = Nbh;   //
			sendBuf[8] = 3;     // 0x03 is required for read for some reason
			sendBuf[12] = Ntl;  // number of tags to read
			sendBuf[13] = Nth;  //

			// Add tags to read to buffer, 4 bytes each
			foreach (Tag t in Tags)
			{
				int i = (4 * Tags.IndexOf(t)) + 14;
				sendBuf[i] = (byte)t.type;
				sendBuf[i + 2] = t.Tl;
				sendBuf[i + 3] = t.Th;
			}
			await stream.WriteAsync(sendBuf, 0, Nb);
		}

		/// <summary>
		/// Reads and assembles tag packets.
		/// </summary>
		/// <returns></returns>
		private async Task<byte[]> readBufferAsync()
		{
			byte[] inBuf = new byte[1490];
			int len = 0;
			try
			{
				len = await stream.ReadAsync(inBuf, 0, 1490);

				if (len >= 1490)
				{
					byte[] _inBuf = await readBufferAsync();
					byte[] sizedBuf = new byte[_inBuf.Length + inBuf.Length];
					Array.Copy(inBuf, 0, sizedBuf, 0, inBuf.Length);
					Array.Copy(_inBuf, 0, sizedBuf, len, _inBuf.Length);

					return sizedBuf;
				}
				byte[] recieved = new byte[len];
				Array.Copy(inBuf, recieved, len);
				return recieved;
			}

			catch (Exception)
			{
				byte[] fail = new byte[4096];
				return fail;
			}
		}

		/// <summary>
		/// Reads strings after tag packets.
		/// </summary>
		/// <param name="t"></param>
		private async Task readStringAsync(Tag t)
		{
			byte[] sendBuf = new byte[14];

			sendBuf[0] = 14;
			sendBuf[8] = 7;
			sendBuf[12] = t.Tl;
			sendBuf[13] = t.Th;

			await stream.WriteAsync(sendBuf, 0, 14);

			byte[] inBuf = new byte[255];

			try
			{
				int len = await stream.ReadAsync(inBuf, 0, 255);
				byte[] sizedBuf = new byte[len - 14];
				Array.Copy(inBuf, 12, sizedBuf, 0, len - 14);
				t.StringValue = System.Text.Encoding.Unicode.GetString(sizedBuf);
			}
			catch
			{
				t.StringValue = "ERROR!";
			}
		}
		
		
		/// <summary>
		/// Reads all tags in list.
		/// </summary>
		public void ReadTags()
		{
			requestReadTags();
			byte[] tagArray = readBuffer();
			List<Tag> Strings = new List<Tag>();

			for (int j = 0; j < Tags.Count; j++)
			{
				Tag t = Tags.ElementAt(j);
				int i = (6 * j) + 14;
				Array.Copy(tagArray, i, t.ByteValue, 0, 4);

				if (t.type == tType.StringType) Strings.Add(t);
			}

			foreach (Tag s in Strings)
			{
				readString(s);
			}
		}

		/* Sends packet requesting tag values, using the structure:
		 * 
		 * 2 bytes for total number of data bits in packet, followed by 6 nulls,
		 * "3" followed by 3 nulls, then 2 bytes for number of tags requested
		 * {Nb, nB, 0 , 0 , 0 , 0 , 0 , 0 , }
		 * {3 , 0 , 0 , 0 , Nt, Nt,} (14 bytes)
		 * Repeated for each tag: 
		 * Byte for tag type followed by null
		 * 2 bytes for tag number
		 * {Tt, 0 , Tn, Tn, } (4 bytes each)
		 * */
		/// <summary>
		/// Requests tag values.
		/// </summary>
		private void requestReadTags()
		{
			Int16 Nb; // Number of bytes in data
			byte Nbh; // Nb high byte
			byte Nbl; // nb low byte
			Int16 Nt; // Number of tags to read
			byte Ntl; // Nt low byte
			byte Nth; // Nt high byte

			Nt = (Int16)Tags.Count;
			Ntl = (byte)Nt;
			Nth = (byte)((Nt & 0xFF00) >> 8);
			Nb = (Int16)((Nt * 4) + 14);
			Nbl = (byte)Nb;
			Nbh = (byte)((Nb & 0xFF00) >> 8);

			// First 14 bytes
			byte[] sendBuf = new byte[Nb];
			sendBuf[0] = Nbl;   // total number of data bytes
			sendBuf[1] = Nbh;   //
			sendBuf[8] = 3;     // 0x03 is required for read for some reason
			sendBuf[12] = Ntl;  // number of tags to read
			sendBuf[13] = Nth;  //

			// Add tags to read to buffer, 4 bytes each
			foreach (Tag t in Tags)
			{
				int i = (4 * Tags.IndexOf(t)) + 14;
				sendBuf[i] = (byte)t.type;
				sendBuf[i + 2] = t.Tl;
				sendBuf[i + 3] = t.Th;
			}
			stream.Write(sendBuf, 0, Nb);
		}

		/// <summary>
		/// Reads and assembles tag packets.
		/// </summary>
		/// <returns></returns>
		private byte[] readBuffer()
		{
			byte[] inBuf = new byte[1490];
			int len = 0;
			try
			{
				len = stream.Read(inBuf, 0, 1490);

				if (len >= 1490)
				{
					byte[] _inBuf = readBuffer();
					byte[] sizedBuf = new byte[_inBuf.Length + inBuf.Length];
					Array.Copy(inBuf, 0, sizedBuf, 0, inBuf.Length);
					Array.Copy(_inBuf, 0, sizedBuf, len, _inBuf.Length);

					return sizedBuf;
				}
				byte[] recieved = new byte[len];
				Array.Copy(inBuf, recieved, len);
				return recieved;
			}

			catch (Exception)
			{
				byte[] fail = new byte[4096];
				return fail;
			}
		}

		/// <summary>
		/// Reads strings after tag packets.
		/// </summary>
		/// <param name="t"></param>
		private void readString(Tag t)
		{
			byte[] sendBuf = new byte[14];

			sendBuf[0] = 14;
			sendBuf[8] = 7;
			sendBuf[12] = t.Tl;
			sendBuf[13] = t.Th;

			stream.Write(sendBuf, 0, 14);

			byte[] inBuf = new byte[255];

			try
			{
				int len = stream.Read(inBuf, 0, 255);
				byte[] sizedBuf = new byte[len - 14];
				Array.Copy(inBuf, 12, sizedBuf, 0, len - 14);
				t.StringValue = System.Text.Encoding.Unicode.GetString(sizedBuf);
			}
			catch
			{
				t.StringValue = "ERROR!";
			}
		}
		

		/// <summary>
		/// Returns tag value.
		/// </summary>
		public object GetTagValue(tType tagType, Int16 tagNumber, string tagName)
		{
			return GetTag(tagType, tagNumber, tagName).getValue();
		}
		/// <summary>
		/// Returns tag value.
		/// </summary>
		public object GetTagValue(tType tagType, Int16 tagNumber)
		{
			return GetTag(tagType, tagNumber).getValue();
		}
		/// <summary>
		/// Returns tag value.
		/// </summary>
		public object GetTagValue(string tagName)
		{
			return GetTag(tagName).getValue();
		}


		/// <summary>
		/// Returns tag in Tags
		/// </summary>
		public Tag GetTag(tType tagType, Int16 tagNumber, string tagName)
		{
			Tag tag = new Tag(tagType, tagNumber, tagName);
			int n = Tags.IndexOf(tag);
			tag = Tags[n];
			return tag;
		}
		/// <summary>
		/// Returns tag in Tags
		/// </summary>
		public Tag GetTag(tType tagType, Int16 tagNumber)
		{
			Int16[] taginfo = { (byte)tagType, tagNumber };
			int index;
			getTagsByNumber.TryGetValue(taginfo, out index);
			Tag tag = Tags[index];
			return tag;
		}
		/// <summary>
		/// Returns tag in Tags
		/// </summary>
		public Tag GetTag(string tagName)
		{
			int index = 0;
			getTagsByString.TryGetValue(tagName, out index);
			Tag tag = Tags[index];
			return tag;
		}


		/// <summary>
		/// Sets tag to specified value, but does not write to PLC.
		/// </summary>
		public void SetTagValue(tType tagType, Int16 tagNumber, string tagName, object tagValue)
		{
			Tag tag = GetTag(tagType, tagNumber, tagName);
			setTagValue(tag, tagValue);
		}
		/// <summary>
		/// Sets tag to specified value, but does not write to PLC.
		/// </summary>
		public void SetTagValue(tType tagType, Int16 tagNumber, object tagValue)
		{
			Tag tag = GetTag(tagType, tagNumber);
			setTagValue(tag, tagValue);
		}
		/// <summary>
		/// Sets tag to specified value, but does not write to PLC.
		/// </summary>
		public void SetTagValue(string tagName, object tagValue)
		{
			Tag tag = GetTag(tagName);
			setTagValue(tag, tagValue);
		}
		/// <summary>
		/// Sets tag value based on tag type.
		/// </summary>
		private void setTagValue(Tag t, object tagValue)
		{
			switch (t.type)
			{
				case tType.InputType:
				case tType.FlagType:
				case tType.OutputType:
					if (tagValue is bool)
					{
						if ((bool)tagValue) t.ByteValue[3] = 1;
						if (!(bool)tagValue) t.ByteValue[3] = 0;
					}
					else throw new System.ArgumentException("tagValue passed is not Boolean");
					break;
				case tType.TimerType:
					if (tagValue is UInt32)
					{
						t.ByteValue = BitConverter.GetBytes(Convert.ToUInt32(tagValue));
					}
					else throw new System.ArgumentException("tagValue passed is not UInt32");
					break;
				case tType.CounterType:
					if (tagValue is Int16)
					{
						t.ByteValue = BitConverter.GetBytes(Convert.ToInt16(tagValue));
					}
					else throw new System.ArgumentException("tagValue passed is not Int16");
					break;
				case tType.NumberType:
					if (tagValue is Int32)
					{
						t.ByteValue = BitConverter.GetBytes(Convert.ToInt32(tagValue));
					}
					else throw new System.ArgumentException("tagValue passed is not Int32");
					break;
				case tType.FloatType:
					if (tagValue is float)
					{
						t.ByteValue = BitConverter.GetBytes(Convert.ToSingle(tagValue));
					}
					else throw new System.ArgumentException("tagValue passed is not float");
					break;
				case tType.StringType:
					if (tagValue is string)
					{
						t.StringValue = (string)tagValue;
					}
					else throw new System.ArgumentException("tagValue passed is not string");
					break;
				default:
					break;
			}

			if (tagValue is string)
			{
				t.StringValue = (string)tagValue;
			}
			else
			{
				t.ByteValue = BitConverter.GetBytes(Convert.ToInt32(tagValue));
			}
		}


		/* 
		 * 
		 * 
		 * ------------------------------------------Write Methods------------------------------------------
		 * 
		 * 
		 * */

		/// <summary>
		/// Sets tag value and writes to PLC
		/// </summary>
		public async Task WriteTagValueAsync(tType tagType, Int16 tagNumber, string tagName, object tagValue)
		{
			Tag tag = GetTag(tagType, tagNumber, tagName);
			setTagValue(tag, tagValue);
			await WriteTagAsync(tag);
		}
		/// <summary>
		/// Sets tag value and writes to PLC
		/// </summary>
		public async Task WriteTagValueAsync(tType tagType, Int16 tagNumber, object tagValue)
		{
			Tag tag = GetTag(tagType, tagNumber);
			setTagValue(tag, tagValue);
			await WriteTagAsync(tag);
		}
		/// <summary>
		/// Sets tag value and writes to PLC
		/// </summary>
		public async Task WriteTagValueAsync(string tagName, object tagValue)
		{
			Tag tag = GetTag(tagName);
			setTagValue(tag, tagValue);
			await WriteTagAsync(tag);
		}
		/// <summary>
		/// Sets tag value and writes to PLC
		/// </summary>
		public async Task WriteTagValueAsync(Tag tag, object tagValue)
		{
			setTagValue(tag, tagValue);
			await WriteTagAsync(tag);
		}

		/// <summary>
		/// Writes all tags passed in tagList.
		/// </summary>
		public async Task WriteTagsAsync(List<Tag> tagList)
		{
			Int16 Nb = (Int16)(14 + (tagList.Count * 8));
			byte Nbl = (byte)Nb;
			byte Nbh = (byte)((Nb & 0xFF00) >> 8);
			byte Tcl = (byte)tagList.Count;
			byte Tch = (byte)((tagList.Count) >> 8);
			byte[] sendBuf = new byte[Nb];

			sendBuf[0] = Nbl;
			sendBuf[1] = Nbh;
			sendBuf[8] = 4;
			sendBuf[12] = Tcl;
			sendBuf[13] = Tch;

			List<Tag> Strings = new List<Tag>();

			// Build sendBuf and Strings tagtagList
			foreach (Tag t in tagList)
			{
				int i = (8 * tagList.IndexOf(t)) + 14;
				sendBuf[i] = (byte)t.type;
				sendBuf[i + 2] = t.Tl;
				sendBuf[i + 3] = t.Th;
				// Strings are zero in this sendBuf
				if (t.type != tType.StringType)
				{
					byte[] val = new byte[4];
					sendBuf[i + 4] = t.ByteValue[0];
					sendBuf[i + 5] = t.ByteValue[1];
					sendBuf[i + 6] = t.ByteValue[2];
					sendBuf[i + 7] = t.ByteValue[3];
				}
				else
				{
					Strings.Add(t);
				}
			}

			foreach (Tag s in Strings)
			{
				await writeStringAsync(s);
			}

			await stream.WriteAsync(sendBuf, 0, Nb);

			int len = 14 + (2 * tagList.Count);
			byte[] inBuf = new byte[len];
			try
			{
				await stream.ReadAsync(inBuf, 0, len);
			}
			catch (Exception)
			{
				//do nothing for now
			}
		}

		/// <summary>
		/// Writes all tags at once
		/// </summary>
		public async Task WriteTagsAsync()
		{
			Int16 Nb = (Int16)(14 + (Tags.Count * 8));
			byte Nbl = (byte)Nb;
			byte Nbh = (byte)((Nb & 0xFF00) >> 8);
			byte Tcl = (byte)Tags.Count;
			byte Tch = (byte)((Tags.Count) >> 8);
			byte[] sendBuf = new byte[Nb];

			sendBuf[0] = Nbl;
			sendBuf[1] = Nbh;
			sendBuf[8] = 4;
			sendBuf[12] = Tcl;
			sendBuf[13] = Tch;

			List<Tag> Strings = new List<Tag>();

			// Build sendBuf and Strings taglist
			foreach (Tag t in Tags)
			{
				int i = (8 * Tags.IndexOf(t)) + 14;
				sendBuf[i] = (byte)t.type;
				sendBuf[i + 2] = t.Tl;
				sendBuf[i + 3] = t.Th;
				// Strings are zero in this sendBuf
				if (t.type != tType.StringType)
				{
					byte[] val = new byte[4];
					sendBuf[i + 4] = t.ByteValue[0];
					sendBuf[i + 5] = t.ByteValue[1];
					sendBuf[i + 6] = t.ByteValue[2];
					sendBuf[i + 7] = t.ByteValue[3];
				}
				else
				{
					Strings.Add(t);
				}
			}

			foreach (Tag s in Strings)
			{
				await writeStringAsync(s);
			}

			await stream.WriteAsync(sendBuf, 0, Nb);

			int len = 14 + (2 * Tags.Count);
			byte[] inBuf = new byte[len];
			try
			{
				await stream.ReadAsync(inBuf, 0, len);
			}
			catch (Exception)
			{
				//do nothing for now
			}
		}

		/// <summary>
		/// Writes specified tag
		/// </summary>
		public async Task WriteTagAsync(Tag tag)
		{
			Int16 Nb = (Int16)(22);
			byte Nbl = (byte)Nb;
			byte Tcl = 1;
			byte[] sendBuf = new byte[Nb];

			sendBuf[0] = Nbl;
			sendBuf[8] = 4;
			sendBuf[12] = Tcl;

			// Build sendBuf and Strings taglist
			int i = 14;
			sendBuf[i] = (byte)tag.type;
			sendBuf[i + 2] = tag.Tl;
			sendBuf[i + 3] = tag.Th;
			// Strings are zero in this sendBuf
			if (tag.type != tType.StringType)
			{
				byte[] val = new byte[4];
				sendBuf[i + 4] = tag.ByteValue[0];
				sendBuf[i + 5] = tag.ByteValue[1];
				sendBuf[i + 6] = tag.ByteValue[2];
				sendBuf[i + 7] = tag.ByteValue[3];
			}
			else
			{
				await writeStringAsync(tag);
			}


			await stream.WriteAsync(sendBuf, 0, Nb);

			int len = 14 + (2 * Tags.Count);
			byte[] inBuf = new byte[len];
			try
			{
				await stream.ReadAsync(inBuf, 0, len);
			}
			catch (Exception)
			{
				//do nothing for now
			}
		}

		/// <summary>
		/// Writes string.
		/// </summary>
		/// <param name="t"></param>
		private async Task writeStringAsync(Tag t)
		{
			if (t.type != tType.StringType)
			{
				//wrong type, throw exception
			}
			// Calculate number of bytes
			Int16 Nb = (Int16)(14 + (t.StringValue.Length * 2));
			byte Nbl = (byte)Nb;
			byte Nbh = (byte)((Nb & 0xFF00) >> 8);
			byte[] sendBuf = new byte[Nb];

			sendBuf[0] = Nbl;
			sendBuf[1] = Nbh;
			sendBuf[8] = 8;
			sendBuf[12] = t.Tl;
			sendBuf[13] = t.Th;

			// Convert string to bytes and copy to send array
			byte[] stringBuf = new byte[t.StringValue.Length * sizeof(char)];
			System.Buffer.BlockCopy(t.StringValue.ToCharArray(), 0, stringBuf, 0, stringBuf.Length);
			Array.Copy(stringBuf, 0, sendBuf, 14, stringBuf.Length);

			await stream.WriteAsync(sendBuf, 0, Nb);

			byte[] inBuf = new byte[35];
			try
			{
				await stream.ReadAsync(inBuf, 0, 35);
			}
			catch (Exception)
			{
				//do nothing for now
			}
			// should return: 0e 00 00 00 00 00 00 00  00 00 00 00 01 00
			// (14 bytes)
		}

	}
}
