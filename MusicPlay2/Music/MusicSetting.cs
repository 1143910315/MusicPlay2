using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;

namespace MusicPlay2.Music {
    public class MusicSetting {
        private static MusicSetting instance;
        private FileInfo fileInfoA = new FileInfo("settingA.set");//原版配置文件
        private FileInfo fileInfoB = new FileInfo("settingB.set");//中间修改配置文件
        private FileInfo fileInfoC = new FileInfo("settingC.set");//原版配置文件的备份
        private FileStream operationFileStream;
        private readonly long startPosition;
        private readonly uint version;
        private struct SampleData {
            public uint dataId;
            public uint dataLength;
        }
        private MusicSetting() {
            if (!fileInfoA.Exists && !fileInfoB.Exists && !fileInfoC.Exists) {
                FileStream temp = fileInfoA.Create();
                byte[] fileSign = Encoding.ASCII.GetBytes("Sv0001");
                temp.Write(fileSign, 0, fileSign.Length);
                temp.Close();
                fileInfoA = new FileInfo("settingA.set");
            }
            if (fileInfoA.Exists) {
                fileInfoB = fileInfoA.CopyTo(fileInfoB.FullName, true);
                operationFileStream = fileInfoB.Open(FileMode.Open, FileAccess.ReadWrite, FileShare.None);
                byte[] fileSign = new byte[6];
                int l = operationFileStream.Read(fileSign, 0, fileSign.Length);
                if (l == fileSign.Length) {
                    if (Enumerable.SequenceEqual(fileSign, Encoding.ASCII.GetBytes("Sv0001"))) {
                        startPosition = operationFileStream.Position;
                        version = 1;
                    }
                }
            }
        }
        public static MusicSetting Instance {
            get {
                if (instance == null) {
                    instance = new MusicSetting();
                }
                return instance;
            }
        }
        //id为0时，移动到文件最开始
        //id为1时，检查文件是否包含错误
        //id为2时，表示该块删除
        public bool MovePositionById(uint id) {
            if (version == 1) {
                _ = operationFileStream.Seek(startPosition, SeekOrigin.Begin);
                if (id == 1) {
                    while (operationFileStream.Position < operationFileStream.Length) {
                        SampleData sampleData = RawDeserializeFromFileStream<SampleData>(operationFileStream);
                        if (operationFileStream.Position + sampleData.dataLength > operationFileStream.Length) {
                            throw new Exception("指定的数据长度超出了文件！");
                        }
                        if (sampleData.dataId < 256) {
                            throw new Exception("数据ID不能小于256！");
                        }
                        _ = operationFileStream.Seek(sampleData.dataLength, SeekOrigin.Current);
                    }
                    return true;
                } else if (id >= 256) {//保留256个ID不用
                    while (operationFileStream.Position < operationFileStream.Length) {
                        long previous = operationFileStream.Position;
                        SampleData sampleData = RawDeserializeFromFileStream<SampleData>(operationFileStream);
                        if (sampleData.dataId == id) {
                            operationFileStream.Position = previous;
                            return true;
                        } else {
                            _ = operationFileStream.Seek(sampleData.dataLength, SeekOrigin.Current);
                        }
                    }
                } else {
                    return true;
                }
            }
            return false;
        }
        public (uint id, List<object>) Read(string parameter) {
            List<object> list = new List<object>();
            string temp = parameter;
            SampleData sampleData = RawDeserializeFromFileStream<SampleData>(operationFileStream);
            byte[] b = new byte[sampleData.dataLength];
            int l = operationFileStream.Read(b, 0, b.Length);
            int startIndex = 0;
            if (l < b.Length) {
                return (0, null);
            }
            try {
                while (temp.Length > 0) {
                    if (temp.StartsWith("S")) {
                        int stringLength = RawDeserialize<int>(b, ref startIndex);
                        list.Add(Encoding.Unicode.GetString(b, startIndex, stringLength));
                        startIndex += stringLength;
                        temp = temp[1..];
                    } else if (temp.StartsWith("I")) {
                        list.Add(RawDeserialize<int>(b, ref startIndex));
                        temp = temp[1..];
                    }
                }
            } catch (Exception) {
                return (0, null);
            }
            return (sampleData.dataId, list);
        }
        public void Write(uint id, string parameter, List<object> list) {
            List<byte> byteSource = new List<byte>();
            string temp = parameter;
            int i = 0;
            while (temp.Length > 0) {
                if (temp.StartsWith("S")) {
                    byte[] tempByte = Encoding.Unicode.GetBytes(list[i++].ToString());
                    byteSource.AddRange(RawSerialize(tempByte.Length));
                    byteSource.AddRange(tempByte);
                    temp = temp[1..];
                } else if (temp.StartsWith("I")) {
                    byteSource.AddRange(RawSerialize(list[i++]));
                    temp = temp[1..];
                }
            }
            MoveNextWrite(id, byteSource.Count);
            operationFileStream.Write(byteSource.ToArray(), 0, byteSource.Count);
        }
        private void MoveNextWrite(uint id, int needLength) {
            if (version == 1) {
                _ = operationFileStream.Seek(startPosition, SeekOrigin.Begin);
                if (id < 256) {
                    throw new Exception("数据ID不能小于256");
                }
                bool found = false;
                long writePosition = 0;
                while (operationFileStream.Position < operationFileStream.Length) {
                    long previous = operationFileStream.Position;
                    SampleData sampleData = RawDeserializeFromFileStream<SampleData>(operationFileStream);
                    if (sampleData.dataId == 2 && sampleData.dataLength >= needLength && !found) {
                        long now = operationFileStream.Position;
                        operationFileStream.Position = previous;
                        byte[] b = RawSerialize(id);
                        operationFileStream.Write(b, 0, b.Length);
                        operationFileStream.Position = now;
                        found = true;
                        writePosition = operationFileStream.Position;
                    }
                    if (sampleData.dataId == id) {
                        if (sampleData.dataLength >= needLength && !found) {
                            found = true;
                            writePosition = operationFileStream.Position;
                        } else {
                            long now = operationFileStream.Position;
                            operationFileStream.Position = previous;
                            byte[] b = RawSerialize(2);
                            operationFileStream.Write(b, 0, b.Length);
                            operationFileStream.Position = now;
                        }
                    }
                    _ = operationFileStream.Seek(sampleData.dataLength, SeekOrigin.Current);
                }
                if (found) {
                    operationFileStream.Position = writePosition;
                } else {
                    operationFileStream.Write(RawSerialize(new SampleData() { dataId = id, dataLength = (uint)needLength }));
                }
            } else {
                throw new Exception("无法写入文件！");
            }
        }
        public uint Append(string parameter, List<object> list) {
            List<byte> byteSource = new List<byte>();
            string temp = parameter;
            int i = 0;
            while (temp.Length > 0) {
                if (temp.StartsWith("S")) {
                    byte[] tempByte = Encoding.Unicode.GetBytes(list[i++].ToString());
                    byteSource.AddRange(RawSerialize(tempByte.Length));
                    byteSource.AddRange(tempByte);
                    temp = temp[1..];
                } else if (temp.StartsWith("I")) {
                    byteSource.AddRange(RawSerialize(list[i++]));
                    temp = temp[1..];
                }
            }
            uint id = MoveNextWrite(byteSource.Count);
            operationFileStream.Write(byteSource.ToArray(), 0, byteSource.Count);
            return id;
        }
        private uint MoveNextWrite(int needLength) {
            if (version == 1) {
                _ = operationFileStream.Seek(startPosition, SeekOrigin.Begin);
                HashSet<uint> set = new HashSet<uint>();
                Random random = new Random();
                uint maxRange = 255;
                bool found = false;
                long idPosition = 0;
                long writePosition = 0;
                while (operationFileStream.Position < operationFileStream.Length) {
                    long previous = operationFileStream.Position;
                    SampleData sampleData = RawDeserializeFromFileStream<SampleData>(operationFileStream);
                    if (sampleData.dataId == 2 && sampleData.dataLength >= needLength && !found) {
                        found = true;
                        idPosition = previous;
                        writePosition = operationFileStream.Position;
                    }
                    if (sampleData.dataId > maxRange) {
                        long times = sampleData.dataId - maxRange - 1;
                        times = Math.Min(10, times);
                        for (long i = 0; i < times; i++) {
                            long c = sampleData.dataId - maxRange - 1;
                            long d = (long)Math.Floor(random.NextDouble() * c) + maxRange + 1;
                            uint number = (uint)d;
                            set.Add(number);
                        }
                        maxRange = sampleData.dataId;
                    }
                    _ = set.Remove(sampleData.dataId);
                    _ = operationFileStream.Seek(sampleData.dataLength, SeekOrigin.Current);
                }
                uint id = maxRange + 1;
                if (set.Count > 0) {
                    id = set.First();
                }
                if (found) {
                    operationFileStream.Position = idPosition;
                    byte[] b = RawSerialize(id);
                    operationFileStream.Write(b, 0, b.Length);
                    operationFileStream.Position = writePosition;
                } else {
                    operationFileStream.Write(RawSerialize(new SampleData() { dataId = id, dataLength = (uint)needLength }));
                }
                return id;
            } else {
                throw new Exception("无法写入文件！");
            }
        }
        public void Delete(uint id) {
            DeleteId(id);
        }
        private void DeleteId(uint id) {
            if (version == 1) {
                _ = operationFileStream.Seek(startPosition, SeekOrigin.Begin);
                while (operationFileStream.Position < operationFileStream.Length) {
                    long previous = operationFileStream.Position;
                    SampleData sampleData = RawDeserializeFromFileStream<SampleData>(operationFileStream);
                    if (id == sampleData.dataId) {
                        long now = operationFileStream.Position;
                        operationFileStream.Position = previous;
                        byte[] b = RawSerialize(2);
                        operationFileStream.Write(b, 0, b.Length);
                        operationFileStream.Position = now;
                    }
                }
            } else {
                throw new Exception("无法写入文件！");
            }
        }
        public bool HasData() {
            return operationFileStream.Length > operationFileStream.Position;
        }
        public void Save() {
            if (operationFileStream != null) {
                operationFileStream.Close();
                operationFileStream = null;
                string originFileName = fileInfoA.FullName;
                string modifyFileName = fileInfoB.FullName;
                fileInfoA.MoveTo(fileInfoC.FullName, true);
                fileInfoC = fileInfoA;
                fileInfoB.MoveTo(originFileName);
                fileInfoA = fileInfoB;
                fileInfoB = fileInfoA.CopyTo(modifyFileName, true);
                operationFileStream = fileInfoB.Open(FileMode.Open, FileAccess.ReadWrite, FileShare.None);
            }
        }
        ~MusicSetting() {
            if (operationFileStream != null) {
                operationFileStream.Close();
            }
        }
        //序列化
        private byte[] RawSerialize(object obj) {
            int rawsize = Marshal.SizeOf(obj);
            IntPtr buffer = Marshal.AllocHGlobal(rawsize);
            Marshal.StructureToPtr(obj, buffer, false);
            byte[] rawdatas = new byte[rawsize];
            Marshal.Copy(buffer, rawdatas, 0, rawsize);
            Marshal.FreeHGlobal(buffer);
            return rawdatas;
        }
        //反序列化
        private T RawDeserialize<T>(byte[] rawdatas, ref int startIndex) {
            int rawsize = Marshal.SizeOf<T>();
            if (startIndex + rawsize > rawdatas.Length) {
                throw new Exception("byte数组长度不足读取该类型！");
            }
            IntPtr buffer = Marshal.AllocHGlobal(rawsize);
            Marshal.Copy(rawdatas, startIndex, buffer, rawsize);
            T retobj = Marshal.PtrToStructure<T>(buffer);
            Marshal.FreeHGlobal(buffer);
            startIndex += rawsize;
            return retobj;
        }
        //从文件流当前位置反序列化一个对象
        private T RawDeserializeFromFileStream<T>(FileStream fileStream) {
            int rawsize = Marshal.SizeOf<T>();
            byte[] rawdatas = new byte[rawsize];
            if (fileStream.Read(rawdatas, 0, rawdatas.Length) < rawsize) {
                throw new Exception("意外到达文件尾！");
            }
            IntPtr buffer = Marshal.AllocHGlobal(rawsize);
            Marshal.Copy(rawdatas, 0, buffer, rawsize);
            T retobj = Marshal.PtrToStructure<T>(buffer);
            Marshal.FreeHGlobal(buffer);
            return retobj;
        }
    }
}
