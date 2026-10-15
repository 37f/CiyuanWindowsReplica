using System;
using System.Collections.Generic;
using System.Linq;
using CiyuanWindows;
class LiveApiCheck {
    static int Main(string[] args) {
        try {
            using(var api=new ApiClient()) {
                object config=api.InitializeAsync().GetAwaiter().GetResult();
                object home=api.RequestAsync("GET","/mobile/home",new Dictionary<string,string>{{"source","嘀哩哩"}}).GetAwaiter().GetResult();
                object[] modules=J.Arr(J.Get(home,"animeModules"));
                Console.WriteLine(J.Stringify(J.Map("origin",api.Origin,"host_count",J.Arr(J.Get(config,"hosts")).Length,"home_sections",modules.Length,"home_items",modules.Sum(m=>J.Arr(J.Get(m,"animes")).Length))));
            } return 0;
        } catch(Exception e) {Console.Error.WriteLine(e.ToString());return 1;}
    }
}
