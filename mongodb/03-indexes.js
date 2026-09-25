db.events.createIndex({locationId:1,date:1},{name:'location_date'});
db.events.createIndex({title:'text',description:'text'},{name:'event_text',default_language:'german'});
db.events.createIndex({date:1,_id:1},{name:'event_pagination'});
db.bookings.createIndex({eventId:1,status:1},{name:'event_status'});
db.bookings.createIndex({customerId:1,bookedAt:-1},{name:'customer_history'});
db.customers.createIndex({email:1},{name:'unique_email',unique:true});
db.reviews.createIndex({eventId:1,customerId:1},{name:'one_review_per_customer',unique:true});
print('7 Indexe erstellt (zusätzlich zu _id).');
