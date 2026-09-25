function summary(label,explain) {
    printjson({label,winningPlan:explain.queryPlanner.winningPlan,
        returned:explain.executionStats.nReturned,keysExamined:explain.executionStats.totalKeysExamined,
        docsExamined:explain.executionStats.totalDocsExamined});
}
const location={locationId:ObjectId('000000000000000000000065'),date:{$gte:new Date()}};
summary('Location+Datum: ohne Index ($natural)',db.events.find(location).hint({$natural:1}).explain('executionStats'));
summary('Location+Datum: Compound-Index',db.events.find(location).hint('location_date').explain('executionStats'));
const bookings={eventId:ObjectId('0000000000000000000001f5'),status:'bezahlt'};
summary('Buchungen: ohne Index',db.bookings.find(bookings).hint({$natural:1}).explain('executionStats'));
summary('Buchungen: event_status',db.bookings.find(bookings).hint('event_status').explain('executionStats'));
summary('Textindex: Rock',db.events.find({$text:{$search:'Rock'}}).explain('executionStats'));
print('Textsuche benötigt ihren Textindex. Kein semantisch gleicher $natural-Vergleich möglich.');
print('Hints isolieren den Indexeffekt; bei kleinen Datenmengen darf der Optimizer ohne Hint COLLSCAN wählen.');
