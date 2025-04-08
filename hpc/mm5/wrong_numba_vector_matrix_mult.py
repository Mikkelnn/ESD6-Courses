import numpy as np
import time
from numba import prange, jit, vectorize

def fill(N,K,M,n_exp):
    x = np.random.randint(M, size = (1,N))     # Filling vector
    A = np.random.randint(M, size=(N,N))       # Filling matrix
    if n_exp ==1: 
        print(f'Vector\n {x}')
        print(f'Matrix\n {A}')
    return x, A
    
def timer(f):                                   # Decorator to calculate execution time of a funciton
    def wrapper(*args, **kw):                   # Needed to decorate a function with input arguments
        t_start = time.time()
        result = f(*args, **kw)                 # Calling function
        t_end = time.time()
        return result, t_end-t_start            # Return the result AND the execution time
    return wrapper


@timer       
def numpy_mult(x,A):
    y = np.matmul(x,A)
    print(y)
    return y
    
@timer
@jit(nopython=True, parallel=False)    
def numba_jit_mult(x,A):        
    H, K  = A.shape 
    y = np.zeros(K)
    for n in range(H):
        for k in range(K):
            y[k] += x[0,n]*A[n,k]
    print(y)
    return y


@vectorize(['float32(float32, float32)'], target='cpu')
def vectorized_hadamard(a,b):
    return a*b


@vectorize(['float32(float32, float32)'], target='cpu')
def vectorized_sum(a,b):
    return a+b    
    
if __name__=="__main__":   
    np.random.seed(2089)   # Setting random seed
    N = 10             # Size of vector
    K = N               # No. columns in matrix
    n_vals = 10         # The range of values is [0,n_vals-1]
    n_exp = 50           # Number of realizations
    exec_times = np.zeros(3)
    max_val = 10        # Upper limit for random number generation is max_val-1
    for i in range(n_exp):       
        x, A = fill(N,K, max_val, n_exp)

        # Calculate the product with numpy

        y_numpy, t_numpy = numpy_mult(x,A)


        # Calculate the product with JIT
        
        y_jit, t_jit = numba_jit_mult(x,A) 

        
        # Calculating the product with vectorized decorator
        xf = x.copy().astype(np.float32)
        Af = A.copy().astype(np.float32)
        t_start = time.time()
        Y_vector = vectorized_hadamard(xf,Af)
        y_vector = vectorized_sum.reduce(Y_vector, axis=1)
        t_end = time.time()
        t_vector= t_end-t_start
        
        exec_times += [t_numpy, t_jit, t_vector]
    exec_times/=n_exp

    print(f'\n%%%%\nFinished execution\n')
    #if np.allclose(y_numpy,y_jit) and np.allclose(y_numpy,y_vector.astype(int)):   # Uncomment to execute
    if True:
        print(f'All the results are correct\nAverage elapsed time in milliseconds:')
        print('    Numpy: {:0.3e} '.format(exec_times[0]*1e3))
        print('    JIT: {:0.3e} '.format(exec_times[1]*1e3))
        print('    Vectorization: {:0.3e} '.format(exec_times[2]*1e3))
    else:
        print(f'Results are incorrect!!!')     
