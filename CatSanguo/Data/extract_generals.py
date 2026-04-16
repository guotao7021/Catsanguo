import json

with open('generals.json', 'r', encoding='utf-8-sig') as f:
    generals = json.load(f)

# 提取所有武将姓名
names = [g['name'] for g in generals]
print(f"当前武将总数: {len(generals)}")
print("\n所有武将姓名:")
for name in sorted(names, key=lambda x: x.encode('gbk')):
    print(name)
