let newGreeter = fn(greeting) {
  return fn(name) { puts(greeting + " " + name); }
};

let hello = newGreeter("Hello");
hello("dear, future Reader!");