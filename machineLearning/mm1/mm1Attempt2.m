clc; clear; close all;

% Define transmitter location
xt = [-2, -4];

% Define grid for visualization
[x1, x2] = meshgrid(-6:0.01:6, -10:0.01:0);

% Define the constraints
c1 = x1.^2 + (x2 + 4).^2 - 16;  % Must be <= 0
c2 = -(x1 - x2 - 6);  % Must be <= 0

% Compute distance squared from transmitter
d2 = (x1 - xt(1)).^2 + (x2 - xt(2)).^2;
P = 1 ./ d2; % Signal power is inversely proportional to distance squared

% Objective function for shortest distance (Minimize squared distance)
fun_min = @(x) (x(1) - xt(1))^2 + (x(2) - xt(2))^2;

% Objective function for longest distance (Maximize squared distance)
fun_max = @(x) -((x(1) - xt(1))^2 + (x(2) - xt(2))^2); % Negate for maximization

% Initial guess for receiver location (inside the feasible region)
x0 = [2; -6];

% Optimization options (stricter constraints)
options = optimoptions('fmincon', 'Algorithm', 'sqp', 'ConstraintTolerance', 1e-6);

% Solve for shortest and longest distance
x_shortest = fmincon(fun_min, x0, [], [], [], [], [], [], @constraints, options);
x_longest = fmincon(fun_max, x0, [], [], [], [], [], [], @constraints, options);

% Plot feasible region
figure; hold on; axis equal;
contourf(x1, x2, (c1 <= 0) & (c2 <= 0), [0.5 0.5], 'g', 'LineWidth', 2);
xlabel('x_1'); ylabel('x_2'); title('Feasible Region and Distances');
colormap([0.8 1 0.8]); % Light green for feasible region
grid on;

% Plot transmitter and receiver locations
plot(xt(1), xt(2), 'ro', 'MarkerSize', 10, 'MarkerFaceColor', 'r'); % Transmitter
plot(x_shortest(1), x_shortest(2), 'bo', 'MarkerSize', 10, 'MarkerFaceColor', 'b'); % Shortest Distance
plot(x_longest(1), x_longest(2), 'mo', 'MarkerSize', 10, 'MarkerFaceColor', 'm'); % Longest Distance

% Draw shortest and longest distance lines
plot([xt(1), x_shortest(1)], [xt(2), x_shortest(2)], 'k--', 'LineWidth', 2); % Shortest path
plot([xt(1), x_longest(1)], [xt(2), x_longest(2)], 'r-', 'LineWidth', 2); % Longest path

% Add legend
legend({'Feasible Region', 'Transmitter', 'Shortest Distance', 'Longest Distance', ...
        'Shortest Path', 'Longest Path'}, 'Location', 'Best');


c3 = x1.^2 + (x2 + 6).^2 - 2; % New constraint: Must be >= 0

% Objective functions
fun_min = @(x) (x(1) - xt(1))^2 + (x(2) - xt(2))^2; % Minimize distance
fun_max = @(x) -((x(1) - xt(1))^2 + (x(2) - xt(2))^2); % Maximize distance

% Solve for shortest and longest distance with new constraints
x_shortest_new = fmincon(fun_min, x0, [], [], [], [], [], [], @constraints_new, options);
x_longest_new = fmincon(fun_max, x0, [], [], [], [], [], [], @constraints_new, options);

% Plot new feasible region
figure; hold on; axis equal;
contourf(x1, x2, (c1 <= 0) & (c2 <= 0) & (c3 >= 0), [0.5 0.5], 'g', 'LineWidth', 2);
xlabel('x_1'); ylabel('x_2'); title('New Feasible Region with Constraint c3');
colormap([0.8 1 0.8]); % Light green for feasible region
grid on;

% Plot transmitter and receiver locations
plot(xt(1), xt(2), 'ro', 'MarkerSize', 10, 'MarkerFaceColor', 'r'); % Transmitter
plot(x_shortest_new(1), x_shortest_new(2), 'bo', 'MarkerSize', 10, 'MarkerFaceColor', 'b'); % Shortest Distance
plot(x_longest_new(1), x_longest_new(2), 'mo', 'MarkerSize', 10, 'MarkerFaceColor', 'm'); % Longest Distance

% Draw shortest and longest distance lines
plot([xt(1), x_shortest_new(1)], [xt(2), x_shortest_new(2)], 'k--', 'LineWidth', 2); % Shortest path
plot([xt(1), x_longest_new(1)], [xt(2), x_longest_new(2)], 'r-', 'LineWidth', 2); % Longest path

% Add legend
legend({'Feasible Region', 'Transmitter', 'Shortest Distance', 'Longest Distance', ...
        'Shortest Path', 'Longest Path'}, 'Location', 'Best');


% =========================
% Constraint Function (Corrected)
% =========================
function [c, ceq] = constraints(x)
    c(1) = -(-x(1)^2 - (x(2) + 4)^2 + 16);  % Circular constraint
    c(2) = -(x(1) - x(2) - 6);              % Linear constraint
    ceq = [];  % No equality constraints
end

function [c, ceq] = constraints_new(x)
    c(1) = -(-x(1)^2 - (x(2) + 4)^2 + 16);  % Circular constraint
    c(2) = -(x(1) - x(2) - 6);              % Linear constraint
    c(3) = -(x(1)^2 + (x(2) + 6)^2 - 2);    % New constraint
    ceq = [];  % No equality constraints
end



