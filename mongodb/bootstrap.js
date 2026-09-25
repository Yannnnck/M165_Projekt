const first = new Mongo('mongodb://localhost:27101/?directConnection=true').getDB('admin');
function commandWorked(result) { if (!result.ok) throw new Error(EJSON.stringify(result)); }
const hello = first.runCommand({hello: 1});
if (!hello.setName) {
    commandWorked(first.runCommand({replSetInitiate: {
        _id: 'eventhub-rs', members: [
            {_id: 0, host: 'localhost:27101'},
            {_id: 1, host: 'localhost:27102'},
            {_id: 2, host: 'localhost:27103'}
        ]
    }}));
}
let primary;
for (let i = 0; i < 90; i++) {
    const h = first.runCommand({hello: 1});
    if (h.primary) { primary = h.primary; break; }
    sleep(1000);
}
if (!primary) throw new Error('Kein Primary gewählt.');
const admin = new Mongo(`mongodb://${primary}/?directConnection=true`).getDB('admin');
let authenticated = false;
try { authenticated = !!admin.auth('admin', process.env.EVENTHUB_ADMIN_PASSWORD); } catch (_) {}
if (!authenticated) {
    // Localhost-Exception: nur möglich, solange noch kein Benutzer existiert.
    admin.createUser({user: 'admin', pwd: process.env.EVENTHUB_ADMIN_PASSWORD, roles: ['root']});
}
print(`Replica Set bereit, Primary: ${primary}`);
