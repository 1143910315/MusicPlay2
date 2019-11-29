using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;

namespace MusicPlay2.Music {
    internal class MusicGroup {
        private static MusicGroup instance;
        private readonly Hashtable musicLists;
        private MusicGroup() {
            MusicSetting musicSetting = MusicSetting.Instance;
            musicLists = new Hashtable();
            _ = musicSetting.MovePositionById(0);
            while (musicSetting.HasData()) {
                (uint id, List<object> list) = musicSetting.Read("IS");
                if (musicLists[list[0]] is MusicList musicList) {
                    musicList.AddFile(id, list[1].ToString());
                } else {
                    musicLists.Add(list[0], new MusicList(id, list[1].ToString()));
                }
            }
        }
        public static MusicGroup Instance {
            get {
                if (instance == null) {
                    instance = new MusicGroup();
                }
                return instance;
            }
        }
    }
}
