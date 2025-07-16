# Deploy StartTranscriptionByTimer to Azure Functions via VS Code

## Quick Deploy (Right-click Deploy)
1. เปิดโฟลเดอร์นี้ใน VS Code
2. ติดตั้ง [Azure Functions Extension](https://marketplace.visualstudio.com/items?itemName=ms-azuretools.vscode-azurefunctions)
3. คลิกขวาที่โฟลเดอร์ StartTranscriptionByTimer แล้วเลือก `Deploy to Function App...`
4. เลือก Subscription, Resource Group, Function App ตามที่ต้องการ
5. รอจน Deploy เสร็จสมบูรณ์

## Notes
- ตรวจสอบให้แน่ใจว่าไฟล์ `function.json`, `host.json`, `.funcignore` และ `local.settings.json` อยู่ครบ
- สามารถแก้ไขค่า environment variable ได้ใน Azure Portal หรือใน local.settings.json ก่อน deploy
- ดู log และสถานะ function ได้จาก Azure Portal หรือ VS Code Azure Extension

## Troubleshooting
- ถ้า Deploy แล้วไม่เห็น Function ใน Azure Portal ให้ตรวจสอบชื่อ entryPoint และ bindings ใน function.json
- ถ้าเจอปัญหาเกี่ยวกับ environment variable ให้แก้ไขใน Azure Portal หรือ redeploy ใหม่
