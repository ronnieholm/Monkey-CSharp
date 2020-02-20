let people = [{"name": "Anna", "age": 24}, {"name": "Bob", "age": 99}];
let getName = fn(person) { person["name"]; };
getName(people[0]);
getName(people[1]);