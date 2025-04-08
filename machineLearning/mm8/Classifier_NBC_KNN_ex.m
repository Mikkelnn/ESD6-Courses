clear all 
close all 
data = readtable('Breast_Cancer.csv');
rng(1234)
% Separate features and labels (Second coloumn containts the labels)
X = data{:, 3:end};  % feature matrix
y = data{:, 2};      % class labels

% If labels are not numeric, convert them.
if ~isnumeric(y)
    y = grp2idx(y)-1; % Convert categorical labels to numeric indices.
    % 0: "M" (Malignant)
    % 1: "B" (Benign)
end


%% Split Data into Training and Test Sets
% Partition the dataset: 70% training, 30% testing.
cv = cvpartition(y, 'HoldOut', 0.3);
X_train = X(training(cv), :);
y_train = y(training(cv));
X_test  = X(test(cv), :);
y_test  = y(test(cv));


classes = unique(y_train);
numClasses = length(classes);
[numTrain, numFeatures] = size(X_train);

% %% Compute the prior probability for each class: 
prior = zeros(numClasses, 1);
%prior(1) is the probability p_C(c=0), and prior(2) is the probability
%p_C(c=1)
for c = 1:numClasses
    idx = (y_train == classes(c));
    N_idx = sum(idx); %Number of class c among 
    % the number of training (numTrain)
    prior(c) = N_idx / numTrain %FILL THE MISSING PART HERE
end

%Gaussian Naive Bayes: (We assume for each class c, that the features are
%independent, and therefore, for each feature x_ we have p(x|c) being a
%gaussian. so for each class, and for each feature, we need to find the
%mean and the variance of the gaussian distributions. We have 2 classes and
%30 features, so we have 2*30 gaussians with 2*30 means and 2*30 variances.
mu = zeros(numClasses, numFeatures);
sigma = zeros(numClasses, numFeatures);

for c = 1:numClasses
    idx = (y_train == classes(c));
    %Features in the data which produced that class: 
    X_c = X_train(idx, :);
    %Use the mean function: by defult, it will do it over the rows of
    %X_c.
    mu(c, :) = mean(X_c,1) %Fill mising code here
    %The same for the variance (use var):
    sigma(c, :) = var(X_c,0,1)%Fill missing code here
end

%% Prediction for test set using Gaussian likelihood
numTest = size(X_test, 1);
y_predGaussian = zeros(numTest, 1);
% We will use the 0-1 loss. For that, optimal decision is to classify based
% on the MAP (the class with the maximum posterior). For that case, we do
% not need to compute the posterior exactly. We can only compute p(x|c)p(c)
% (proportional to the posterior).
for i = 1:numTest
    posteriors = zeros(numClasses, 1);
    for c = 1:numClasses
        likelihood = 1;
        for j = 1:numFeatures
            % Avoid division by zero for near-zero variance
            sigma_val = sigma(c, j);
            if sigma_val == 0
                sigma_val = eps;
            end
            % Gaussian probability density function for feature j
            likelihood = likelihood * (1/sqrt(2*pi*sigma_val)) * ...
                exp(-((X_test(i, j) - mu(c,j))^2)/(2*sigma_val));
        end
        % Multiply by prior probability
        posteriors(c) = likelihood * prior(c);
    end
    % Assign the class with the maximum posterior probability
    [~, idx] = max(posteriors);
    y_predGaussian(i) = classes(idx);
end

%% Evaluate the Gaussian classifier
cmGaussian = confusionmat(y_test, y_predGaussian);
accuracyGaussian = sum(diag(cmGaussian)) / sum(cmGaussian(:));
fprintf('Gaussian Naive Bayes Accuracy: %.2f%%\n', accuracyGaussian * 100);
% Display the confusion matrix as a heat map
figure;
confusionchart(cmGaussian);
title('Confusion Matrix - Gaussian Naive Bayes');


%% Part 2: Kernel Density Estimation (KDE) Naive Bayes Classifier
% For KDE, we will not compute summary statistics. Instead, we store the training
% samples for each class and estimate the density at test time.

% Organize training data by class
X_train_byClass = cell(numClasses, 1);
for c = 1:numClasses
    idx = (y_train == classes(c));
    X_train_byClass{c} = X_train(idx, :);
end

% Set the kernel width (bandwidth). You can experiment with different values.
h = 50;

y_predKernel = zeros(numTest, 1);

for i = 1:numTest
    posteriors = zeros(numClasses, 1);
    for c = 1:numClasses
        likelihood = 1;
        X_class = X_train_byClass{c};
        n_c = size(X_class, 1);
        for j = 1:numFeatures
            % Compute the difference between test sample feature and all training samples
            diff = X_test(i, j) - X_class(:, j);
            % Gaussian kernel density estimation for feature j
            kernel_vals = (1/(h*sqrt(2*pi))) * exp(-(diff.^2)/(2*h^2));
            % Estimated density is the average over training samples for the class
            p_xj = sum(kernel_vals) / n_c;
            likelihood = likelihood * p_xj;
        end
        % Multiply by the prior probability
        posteriors(c) = likelihood * prior(c);
    end
    % Choose the class with the maximum posterior probability
    [~, idx] = max(posteriors);
    y_predKernel(i) = classes(idx);
end

% Evaluate the KDE classifier
cmKernel = confusionmat(y_test, y_predKernel);
accuracyKernel = sum(diag(cmKernel)) / sum(cmKernel(:));
fprintf('Kernel Naive Bayes (h = %.2f) Accuracy: %.2f%%\n', h, accuracyKernel * 100);

% Display the confusion matrix for the KDE classifier
figure;
confusionchart(cmKernel);
title(sprintf('Confusion Matrix - Kernel Naive Bayes (h = %.2f)', h));

%% Train the KNN Classifier
% Set the number of neighbors (experiment with different values, e.g., 3, 5, 7)
numNeighbors = 10;
knnModel = fitcknn(X_train, y_train, 'NumNeighbors', numNeighbors);

% Predict the labels for the test set
y_predKNN = predict(knnModel, X_test);

% Compute the confusion matrix
cmKNN = confusionmat(y_test, y_predKNN);

% Calculate accuracy
accuracyKNN = sum(diag(cmKNN)) / sum(cmKNN(:));
fprintf('KNN (NumNeighbors = %d) Accuracy: %.2f%%\n', numNeighbors, accuracyKNN * 100);

% Display the confusion matrix as a heat map
figure;
confusionchart(cmKNN);
title(sprintf('Confusion Matrix - KNN (NumNeighbors = %d)', numNeighbors));