// Passwörter stehen ausschliesslich in lokalen Umgebungsvariablen, nie im Repository.
const role = process.env.EVENTHUB_ROLE || 'admin';
const password = process.env[`EVENTHUB_${role.toUpperCase()}_PASSWORD`];
if (!password) throw new Error('Lokale Zugangsdaten fehlen. PowerShell-Starter verwenden.');
const hosts = process.env.EVENTHUB_HOSTS;
const uri = `mongodb://${role}:${encodeURIComponent(password)}@${hosts}/eventhub?authSource=admin&replicaSet=eventhub-rs&retryWrites=true&w=majority&serverSelectionTimeoutMS=30000`;
globalThis.db = new Mongo(uri).getDB('eventhub');
load(process.env.EVENTHUB_SCRIPT);
