import os

directory = r"C:\Program Files (x86)\Steam\steamapps\common\Rain World\RainWorld_Data\StreamingAssets\mods\backroomsssss\world\gates\ "
directory = directory[:-1]
source = "si_vs"
target = "bk_vs"
os.rename(directory+f"gate_{source}.txt", directory+f"gate_{target}.txt")
os.rename(directory+f"gate_{source}_1.png", directory+f"gate_{target}_1.png")
os.rename(directory+f"gate_{source}_settings.txt", directory+f"gate_{target}_settings.txt")

quit()

import networkx as nx

G = nx.read_adjlist(path="random_quartic_graph3.adjlist", nodetype = int)

nodeSet = {65, 35, 36, 6, 71, 74, 43, 109, 15, 117, 85, 56, 59, 60, 29, 63}

for node in nodeSet:
    for i in nodeSet:
        length = nx.shortest_path_length(G, source=node, target=i)
        if length == 2:
            print(nx.shortest_path(G, source=node, target=i))

quit()

with open("output.txt", "r") as file:
    text = file.read()

with open("output.txt", "w") as file:
    file.write(text.replace(":", " :"))


quit()

with open("furtherestnodes.txt", "r") as file:
    text1, text2 = file.read().split("#")
    print(sum(1 for _ in filter(lambda x: x == "2", text2)))