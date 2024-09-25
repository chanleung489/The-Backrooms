import networkx as nx
import matplotlib.pyplot as plt


# subax1 = plt.subplot(121)
# nx.draw(G)
# plt.show()
# adjTo0 = list(G.adj[0])
# print("0:", *adjTo0)


# eccentricityDictionary = dict(nx.eccentricity(G))
# print(len(eccentricityDictionary))
# furtherestNodes = sorted(eccentricityDictionary, key=eccentricityDictionary.get)
# print(furtherestNodes[-10:], [eccentricityDictionary[i] for i in furtherestNodes[-10:]])


# numberOfSixes = {}

# for i in range(125):
#     lengths = nx.shortest_path_length(G, source=79)
#     numberOfSixes[str(i)] = sum(1 for _ in filter(lambda x: lengths[x] == 6, lengths))
#     # print(nx.shortest_path(G, source = 1, target = 79))

# # print(sorted(numberOfSixes, key=numberOfSixes.get))
# print(numberOfSixes)


# while 1:
#     # G = nx.random_regular_graph(4, 125)
#     # nx.write_adjlist(G, path="random_quartic_graph3.adjlist")

#     G = nx.read_adjlist(path="random_quartic_graph3.adjlist", nodetype = int)

#     for center in nx.center(G):
#         nodeSet = set()
#         lengths = nx.shortest_path_length(G, source=center)
#         filtered = list(filter(lambda x: lengths[x] == 3, lengths))
#         for i in filtered:
#             lengths = nx.shortest_path_length(G, source=i)
#             opposite = max(lengths, key=lengths.get)
#             if opposite in filtered:
#                 nodeSet.add(i)
#                 nodeSet.add(opposite)

#         if len(nodeSet) < 16: 
#             continue

#         flag = False
#         printFlag = True
#         count = 0

#         for i in nodeSet:
#             if count > 20:
#                 print(count)
#                 flag = True
#             if printFlag: 
#                 print(i, end=": ")
#             for j in nodeSet:
#                 if i == j: 
#                     continue
#                 length = nx.shortest_path_length(G, source=i, target=j)
#                 output = str(length)
#                 if length < 2:
#                     flag = True
#                     break
#                 if length == 2:
#                     output += f"({j})"
#                     count += 1
#                 if printFlag:
#                     print(output, end=", ")
#             if printFlag:
#                 print()
#             if flag:
#                 break

#         if printFlag:
#             print(nodeSet, len(nodeSet), "\n")

#         if not flag: 
#             print("yes", center)
#             quit()

# quit()

G = nx.read_adjlist(path="random_quartic_graph3.adjlist", nodetype = int)
print(nx.shortest_path(G, source=74, target=6))

quit()

output = ""
for i in range(0, 125):
    # print(f"BK_A{str(i+1).zfill(3)}", ": ", ", ".join([f"BK_A{str(j+1).zfill(3)}" for j in list(G.adj[i])]), sep="")
    output += f"BK_A{str(i+1).zfill(3)} : " + ", ".join([f"BK_A{str(j+1).zfill(3)}" for j in list(G.adj[i])]) + "\n"

with open("output.txt", "w") as file:
    file.write(output)
# print(output)