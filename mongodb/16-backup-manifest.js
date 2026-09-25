const fs=require('fs');
load(process.env.EVENTHUB_SCRIPT.replace('16-backup-manifest.js','snapshot-helper.js'));
fs.writeFileSync(process.env.EVENTHUB_BACKUP_MANIFEST,EJSON.stringify(snapshot(db),{relaxed:false}));
print('Backup-Manifest mit Dokument-Hashes, Indexen und Validatoren geschrieben.');
