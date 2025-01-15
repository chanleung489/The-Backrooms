import shutil

directory = r"C:\Program Files (x86)\Steam\steamapps\common\Rain World\RainWorld_Data\StreamingAssets\mods\backroomsssss\world\gates\ "
directory = directory[:-1]
# print(directory)
# quit()

# regions = ["cc", "ds", "gw", "hi", "lf", "sb", "sh", "si", "sl", "ss", "su", "uw", "vs", "oe", "ms"]
# for name in regions:
#     print(f"gate_bk_{name}".upper())
#     shutil.copyfile(directory+"BK_A001.txt", directory+f"gate_bk_{name}.txt".upper())
#     shutil.copyfile(directory+"BK_A001_1.png", directory+f"gate_bk_{name}_1.png".upper())
#     shutil.copyfile(directory+"BK_A001_settings.txt", directory+f"gate_bk_{name}_settings.txt".upper())
# print(len(regions))

# quit()

for i in range(1, 126):
    # i = 0
    i = str(i).zfill(3)
    shutil.copyfile("BK_A00.txt", f"BK_A{i}.txt")
    shutil.copyfile("BK_A00_1.png", f"BK_A{i}_1.png")
    # shutil.copyfile("bk_a00_settings.txt", f"bk_a{i}_settings.txt")

for i in range(1, 17):
    # i = 0
    i = str(i).zfill(3)
    shutil.copyfile("BK_A00.txt", f"BK_S{i}.txt")
    shutil.copyfile("BK_A00_1.png", f"BK_S{i}_1.png")
    # shutil.copyfile("bk_a00_settings.txt", f"bk_s{i}_settings.txt")