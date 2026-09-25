load(process.env.EVENTHUB_SCRIPT.replace('17-verify-restore.js','snapshot-helper.js'));
const fs=require('fs');
const expected=EJSON.parse(fs.readFileSync(process.env.EVENTHUB_BACKUP_MANIFEST,'utf8'));
const restored=db.getSiblingDB(process.env.EVENTHUB_RESTORE_DB);
const actual=snapshot(restored);
if(EJSON.stringify(expected,{relaxed:false})!==EJSON.stringify(actual,{relaxed:false})) throw new Error('Restore stimmt nicht mit dem Backup-Manifest überein.');
for(const item of actual) print(`PASS | Restore ${item.name}: ${item.count} Dokumente, SHA256, Indexe, Validatoren identisch.`);
print('BACKUP UND RESTORE ERFOLGREICH GEPRÜFT.');
